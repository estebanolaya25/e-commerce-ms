import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Marca } from '../../../../core/modelos/catalogo.model';
import { AdminCatalogoService } from '../../../../core/servicios/admin-catalogo.service';
import { AvisosService } from '../../../../core/servicios/avisos.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';

@Component({
  selector: 'app-catalogo-marcas',
  standalone: false,
  templateUrl: './catalogo-marcas.html',
  styleUrl: './catalogo-marcas.scss'
})
export class CatalogoMarcas implements OnInit {
  marcas: Marca[] = [];
  filtro = '';
  cargando = true;
  error: string | null = null;
  mostrarFormulario = false;
  editando: Marca | null = null;
  guardando = false;
  readonly cambiando = new Set<string>();
  readonly formulario;

  constructor(fb: FormBuilder, private adminCatalogo: AdminCatalogoService, private avisos: AvisosService) {
    this.formulario = fb.nonNullable.group({
      nombre: ['', [Validators.required, Validators.maxLength(100)]],
      slug: ['', [Validators.maxLength(120), Validators.pattern(/^[a-z0-9]+(-[a-z0-9]+)*$/)]]
    });
  }

  ngOnInit(): void {
    this.cargar();
  }

  get visibles(): Marca[] {
    const texto = this.filtro.trim().toLowerCase();
    return texto ? this.marcas.filter(m => m.nombre.toLowerCase().includes(texto) || m.slug.includes(texto)) : this.marcas;
  }

  nueva(): void {
    this.editando = null;
    this.formulario.reset({ nombre: '', slug: '' });
    this.mostrarFormulario = true;
  }

  editar(marca: Marca): void {
    this.editando = marca;
    this.formulario.reset({ nombre: marca.nombre, slug: marca.slug });
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
    const peticion = { nombre: valor.nombre.trim(), slug: valor.slug.trim() || null };
    this.guardando = true;
    const llamada = this.editando ? this.adminCatalogo.actualizarMarca(this.editando.id, peticion) : this.adminCatalogo.crearMarca(peticion);

    llamada.subscribe({
      next: marca => {
        this.avisos.exito(this.editando ? 'Marca actualizada' : 'Marca creada', marca.nombre);
        this.guardando = false;
        this.cancelar();
        this.cargar();
      },
      error: error => {
        this.guardando = false;
        this.avisos.error('No se pudo guardar la marca', mensajeDeError(error));
      }
    });
  }

  cambiarEstado(marca: Marca, evento: Event): void {
    const casilla = evento.target as HTMLInputElement;
    const activa = casilla.checked;
    this.cambiando.add(marca.id);
    this.adminCatalogo.cambiarEstadoMarca(marca.id, activa).subscribe({
      next: () => {
        marca.activa = activa;
        this.cambiando.delete(marca.id);
        this.avisos.exito(activa ? 'Marca activada' : 'Marca desactivada', marca.nombre);
      },
      error: error => {
        casilla.checked = marca.activa;
        this.cambiando.delete(marca.id);
        this.avisos.error('No se pudo cambiar el estado', mensajeDeError(error));
      }
    });
  }

  private cargar(): void {
    this.cargando = true;
    this.adminCatalogo.listarMarcas().subscribe({
      next: marcas => {
        this.marcas = [...marcas].sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'));
        this.cargando = false;
      },
      error: error => {
        this.error = mensajeDeError(error, 'No se pudieron cargar las marcas.');
        this.cargando = false;
      }
    });
  }
}
