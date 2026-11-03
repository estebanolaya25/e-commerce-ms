import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Categoria } from '../../../../core/modelos/catalogo.model';
import { AdminCatalogoService } from '../../../../core/servicios/admin-catalogo.service';
import { AvisosService } from '../../../../core/servicios/avisos.service';
import { CategoriaPlana, aplanarCategorias, idsRama } from '../../../../core/utilidades/categorias';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';

@Component({
  selector: 'app-catalogo-categorias',
  standalone: false,
  templateUrl: './catalogo-categorias.html',
  styleUrl: './catalogo-categorias.scss'
})
export class CatalogoCategorias implements OnInit {
  filas: CategoriaPlana[] = [];
  cargando = true;
  error: string | null = null;
  mostrarFormulario = false;
  editando: Categoria | null = null;
  guardando = false;
  readonly cambiando = new Set<string>();
  readonly formulario;

  constructor(fb: FormBuilder, private adminCatalogo: AdminCatalogoService, private avisos: AvisosService) {
    this.formulario = fb.nonNullable.group({
      nombre: ['', [Validators.required, Validators.maxLength(100)]],
      slug: ['', [Validators.maxLength(120), Validators.pattern(/^[a-z0-9]+(-[a-z0-9]+)*$/)]],
      categoriaPadreId: [''],
      descripcion: ['', Validators.maxLength(2000)],
      orden: [0, [Validators.required, Validators.min(0), Validators.max(10000)]]
    });
  }

  ngOnInit(): void {
    this.cargar();
  }

  // Una categoría no puede colgar de sí misma ni de una de sus hijas.
  get padresPosibles(): CategoriaPlana[] {
    if (!this.editando) return this.filas;
    const excluidas = new Set(idsRama(this.editando));
    return this.filas.filter(f => !excluidas.has(f.categoria.id));
  }

  sangria(nivel: number): string {
    return '\u00a0\u00a0'.repeat(nivel * 2) + (nivel ? '› ' : '');
  }

  nueva(padre?: Categoria): void {
    this.editando = null;
    this.formulario.reset({ nombre: '', slug: '', categoriaPadreId: padre?.id ?? '', descripcion: '', orden: 0 });
    this.mostrarFormulario = true;
  }

  editar(categoria: Categoria): void {
    this.editando = categoria;
    this.formulario.reset({
      nombre: categoria.nombre,
      slug: categoria.slug,
      categoriaPadreId: categoria.categoriaPadreId ?? '',
      descripcion: categoria.descripcion ?? '',
      orden: categoria.orden
    });
    this.mostrarFormulario = true;
  }

  cancelar(): void {
    this.mostrarFormulario = false;
    this.editando = null;
  }

  invalido(campo: string): boolean {
    const control = this.formulario.get(campo);
    return !!control && control.invalid && control.touched;
  }

  guardar(): void {
    this.formulario.markAllAsTouched();
    if (this.formulario.invalid) return;

    const valor = this.formulario.getRawValue();
    const peticion = {
      nombre: valor.nombre.trim(),
      slug: valor.slug.trim() || null,
      categoriaPadreId: valor.categoriaPadreId || null,
      descripcion: valor.descripcion.trim() || null,
      orden: valor.orden
    };

    this.guardando = true;
    const llamada = this.editando
      ? this.adminCatalogo.actualizarCategoria(this.editando.id, peticion)
      : this.adminCatalogo.crearCategoria(peticion);

    llamada.subscribe({
      next: categoria => {
        this.avisos.exito(this.editando ? 'Categoría actualizada' : 'Categoría creada', categoria.nombre);
        this.guardando = false;
        this.cancelar();
        this.cargar();
      },
      error: error => {
        this.guardando = false;
        this.avisos.error('No se pudo guardar la categoría', mensajeDeError(error));
      }
    });
  }

  cambiarEstado(categoria: Categoria, evento: Event): void {
    const casilla = evento.target as HTMLInputElement;
    const activa = casilla.checked;
    this.cambiando.add(categoria.id);
    this.adminCatalogo.cambiarEstadoCategoria(categoria.id, activa).subscribe({
      next: () => {
        categoria.activa = activa;
        this.cambiando.delete(categoria.id);
        this.avisos.exito(activa ? 'Categoría activada' : 'Categoría desactivada', categoria.nombre);
      },
      error: error => {
        casilla.checked = categoria.activa;
        this.cambiando.delete(categoria.id);
        this.avisos.error('No se pudo cambiar el estado', mensajeDeError(error));
      }
    });
  }

  private cargar(): void {
    this.cargando = true;
    this.adminCatalogo.listarCategorias().subscribe({
      next: arbol => {
        this.filas = aplanarCategorias(arbol);
        this.cargando = false;
      },
      error: error => {
        this.error = mensajeDeError(error, 'No se pudieron cargar las categorías.');
        this.cargando = false;
      }
    });
  }
}
