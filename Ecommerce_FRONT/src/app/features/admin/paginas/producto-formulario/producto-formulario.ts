import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import {
  ImagenProducto, Marca, ProductoDetalle, ProductoPeticion, Variante, VariantePeticion
} from '../../../../core/modelos/catalogo.model';
import { DescripcionSugerida } from '../../../../core/modelos/asistente.model';
import { AdminCatalogoService } from '../../../../core/servicios/admin-catalogo.service';
import { AsistenteService } from '../../../../core/servicios/asistente.service';
import { AvisosService } from '../../../../core/servicios/avisos.service';
import { CategoriaPlana, aplanarCategorias } from '../../../../core/utilidades/categorias';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';

// Mismas reglas que valida el microservicio de Catálogo.
const PATRON_SKU = /^[A-Za-z0-9_-]+$/;
const PATRON_SLUG = /^[a-z0-9]+(-[a-z0-9]+)*$/;
const TIPOS_IMAGEN = ['image/jpeg', 'image/png', 'image/webp'];
const TAMANO_MAXIMO_IMAGEN_MB = 5;
const MAXIMO_ENTRADAS = 20;

interface Atributo {
  clave: string;
  valor: string;
}

// Una fila de la tabla de variantes. Sin id = todavía no existe en el backend.
interface FilaVariante {
  id: string | null;
  sku: string;
  opciones: Record<string, string>;
  precio: number | null;
  precioAntes: number | null;
  precioEfectivo: number | null;
  activa: boolean;
  editando: boolean;
  guardando: boolean;
  respaldo: { sku: string; opciones: Record<string, string>; precio: number | null; precioAntes: number | null } | null;
}

@Component({
  selector: 'app-producto-formulario',
  standalone: false,
  templateUrl: './producto-formulario.html',
  styleUrl: './producto-formulario.scss'
})
export class ProductoFormulario implements OnInit {
  productoId: string | null = null;
  producto: ProductoDetalle | null = null;
  cargando = true;
  noEncontrado = false;
  guardando = false;
  cambiandoEstado = false;
  intentoGuardar = false;

  categorias: CategoriaPlana[] = [];
  marcas: Marca[] = [];
  atributos: Atributo[] = [];
  nombresOpciones: string[] = [];
  nuevaOpcion = '';
  filas: FilaVariante[] = [];

  archivo: File | null = null;
  textoAlternativo = '';
  errorImagen: string | null = null;
  subiendo = false;
  readonly ocupadas = new Set<string>();

  // Descripción sugerida por el asistente: se muestra como vista previa y solo se copia al formulario si se acepta.
  generandoDescripcion = false;
  sugerencia: DescripcionSugerida | null = null;
  errorSugerencia: string | null = null;

  readonly formulario;

  constructor(
    fb: FormBuilder,
    private adminCatalogo: AdminCatalogoService,
    private asistente: AsistenteService,
    private avisos: AvisosService,
    private ruta: ActivatedRoute,
    private router: Router
  ) {
    this.formulario = fb.nonNullable.group({
      nombre: ['', [Validators.required, Validators.maxLength(200)]],
      slug: ['', [Validators.maxLength(220), Validators.pattern(PATRON_SLUG)]],
      categoriaId: ['', Validators.required],
      marcaId: [''],
      descripcionCorta: ['', Validators.maxLength(500)],
      descripcion: ['', Validators.maxLength(10000)],
      precioBase: [0, [Validators.required, Validators.min(0)]],
      destacado: [false]
    });
  }

  get esNuevo(): boolean {
    return this.productoId === null;
  }

  // ---------- Descripción con el asistente ----------

  // Usa lo que hay en el formulario (aunque no esté guardado): nombre, categoría, marca, atributos y opciones de variantes.
  generarDescripcion(): void {
    const valor = this.formulario.getRawValue();
    const categoria = this.categorias.find(c => c.categoria.id === valor.categoriaId)?.categoria.nombre;
    if (!valor.nombre.trim() || !categoria) {
      this.errorSugerencia = 'Escriba el nombre y elija la categoría antes de generar la descripción.';
      return;
    }

    const opciones: Record<string, string[]> = {};
    for (const fila of this.filas) {
      for (const [nombre, valorOpcion] of Object.entries(fila.opciones)) {
        const limpio = valorOpcion.trim();
        const valores = (opciones[nombre] ??= []);
        if (limpio && !valores.includes(limpio)) {
          valores.push(limpio);
        }
      }
    }

    this.generandoDescripcion = true;
    this.errorSugerencia = null;
    this.sugerencia = null;
    this.asistente.generarDescripcion({
      nombre: valor.nombre.trim(),
      categoria,
      marca: this.marcas.find(m => m.id === valor.marcaId)?.nombre ?? null,
      atributos: Object.fromEntries(this.atributos.filter(a => a.clave.trim() && a.valor.trim()).map(a => [a.clave.trim(), a.valor.trim()])),
      opciones,
      descripcionActual: valor.descripcion.trim() || null
    }).subscribe({
      next: sugerencia => {
        this.sugerencia = sugerencia;
        this.generandoDescripcion = false;
      },
      error: error => {
        this.errorSugerencia = mensajeDeError(error, 'No se pudo generar la descripción. Intente de nuevo.');
        this.generandoDescripcion = false;
      }
    });
  }

  usarSugerencia(): void {
    if (!this.sugerencia) return;
    this.formulario.patchValue({
      descripcionCorta: this.sugerencia.descripcionCorta,
      descripcion: this.sugerencia.descripcion
    });
    this.formulario.markAsDirty();
    this.sugerencia = null;
    this.avisos.info('Descripción aplicada', 'Revísela y guarde el producto para publicarla.');
  }

  descartarSugerencia(): void {
    this.sugerencia = null;
  }

  ngOnInit(): void {
    this.productoId = this.ruta.snapshot.paramMap.get('productoId');

    forkJoin({
      categorias: this.adminCatalogo.listarCategorias(),
      marcas: this.adminCatalogo.listarMarcas(),
      producto: this.productoId ? this.adminCatalogo.obtenerProducto(this.productoId) : of(null)
    }).subscribe({
      next: ({ categorias, marcas, producto }) => {
        this.categorias = aplanarCategorias(categorias);
        this.marcas = marcas;
        if (producto) {
          this.cargarProducto(producto);
        } else {
          this.filas = [this.filaNueva()];
        }
        this.cargando = false;
      },
      error: () => {
        this.noEncontrado = true;
        this.cargando = false;
      }
    });
  }

  invalido(campo: string): boolean {
    const control = this.formulario.get(campo);
    return !!control && control.invalid && (control.touched || this.intentoGuardar);
  }

  sangria(nivel: number): string {
    return '\u00a0\u00a0'.repeat(nivel * 2) + (nivel ? '› ' : '');
  }

  // ---------- Atributos (ficha técnica) ----------
  agregarAtributo(): void {
    if (this.atributos.length < MAXIMO_ENTRADAS) this.atributos.push({ clave: '', valor: '' });
  }

  quitarAtributo(indice: number): void {
    this.atributos.splice(indice, 1);
  }

  get errorAtributos(): string | null {
    const usados = this.atributos.filter(a => a.clave.trim() || a.valor.trim());
    if (usados.some(a => !a.clave.trim() || !a.valor.trim())) return 'Cada atributo necesita nombre y valor.';
    if (usados.some(a => a.clave.trim().length > 50 || a.valor.trim().length > 200)) return 'El nombre admite 50 caracteres y el valor 200.';
    const claves = usados.map(a => a.clave.trim().toLowerCase());
    if (new Set(claves).size !== claves.length) return 'Hay atributos repetidos.';
    return null;
  }

  // ---------- Opciones de variante (Color, Talla, ...) ----------
  agregarOpcion(): void {
    const nombre = this.nuevaOpcion.trim();
    if (!nombre || nombre.length > 50 || this.nombresOpciones.length >= MAXIMO_ENTRADAS) return;
    if (this.nombresOpciones.some(o => o.toLowerCase() === nombre.toLowerCase())) {
      this.avisos.error('Opción repetida', `Ya existe la opción "${nombre}".`);
      return;
    }
    this.nombresOpciones.push(nombre);
    this.nuevaOpcion = '';
  }

  // Solo se puede quitar si ninguna variante ya guardada la usa.
  puedeQuitarOpcion(nombre: string): boolean {
    return !this.filas.some(f => f.id && f.opciones[nombre]);
  }

  quitarOpcion(nombre: string): void {
    if (!this.puedeQuitarOpcion(nombre)) return;
    this.nombresOpciones = this.nombresOpciones.filter(o => o !== nombre);
    this.filas.filter(f => !f.id).forEach(f => delete f.opciones[nombre]);
  }

  // ---------- Variantes ----------
  agregarVariante(): void {
    this.filas.push(this.filaNueva());
  }

  quitarFila(fila: FilaVariante): void {
    this.filas = this.filas.filter(f => f !== fila);
  }

  editarFila(fila: FilaVariante): void {
    fila.respaldo = { sku: fila.sku, opciones: { ...fila.opciones }, precio: fila.precio, precioAntes: fila.precioAntes };
    fila.editando = true;
  }

  cancelarFila(fila: FilaVariante): void {
    if (!fila.id) {
      this.quitarFila(fila);
      return;
    }
    Object.assign(fila, fila.respaldo, { editando: false, respaldo: null });
  }

  errorFila(fila: FilaVariante): string | null {
    const sku = fila.sku.trim();
    if (!sku) return 'Escriba el SKU.';
    if (sku.length > 50 || !PATRON_SKU.test(sku)) return 'El SKU solo admite letras, números, guion y guion bajo (máx. 50).';
    if (this.filas.some(f => f !== fila && f.sku.trim().toUpperCase() === sku.toUpperCase())) return 'SKU repetido en este producto.';
    if (this.nombresOpciones.some(o => (fila.opciones[o] ?? '').trim().length > 200)) return 'Cada opción admite 200 caracteres.';
    if (fila.precio != null && fila.precio < 0) return 'El precio no puede ser negativo.';
    if (fila.precioAntes != null) {
      const precio = fila.precio ?? this.formulario.controls.precioBase.value;
      if (fila.precioAntes <= precio) return 'El precio "antes" debe ser mayor que el precio de venta.';
    }
    return null;
  }

  guardarFila(fila: FilaVariante): void {
    if (!this.productoId || this.errorFila(fila)) return;

    fila.guardando = true;
    const peticion = this.peticionVariante(fila);
    const llamada = fila.id
      ? this.adminCatalogo.actualizarVariante(fila.id, peticion)
      : this.adminCatalogo.agregarVariante(this.productoId, peticion);

    llamada.subscribe({
      next: variante => {
        const nueva = !fila.id;
        Object.assign(fila, this.filaDesde(variante));
        this.avisos.exito(nueva ? 'Variante agregada' : 'Variante actualizada', `SKU ${variante.sku}.`);
      },
      error: error => {
        fila.guardando = false;
        this.avisos.error('No se pudo guardar la variante', mensajeDeError(error));
      }
    });
  }

  cambiarEstadoVariante(fila: FilaVariante, evento: Event): void {
    const casilla = evento.target as HTMLInputElement;
    const activa = casilla.checked;
    fila.guardando = true;
    this.adminCatalogo.cambiarEstadoVariante(fila.id!, activa).subscribe({
      next: () => {
        fila.activa = activa;
        fila.guardando = false;
        this.avisos.exito(activa ? 'Variante activada' : 'Variante desactivada', `SKU ${fila.sku}.`);
      },
      error: error => {
        casilla.checked = fila.activa;
        fila.guardando = false;
        this.avisos.error('No se pudo cambiar la variante', mensajeDeError(error));
      }
    });
  }

  textoOpciones(fila: FilaVariante): string {
    return this.nombresOpciones.map(o => fila.opciones[o]).filter(Boolean).join(' · ') || 'Sin opciones';
  }

  // ---------- Guardar producto ----------
  guardar(): void {
    this.intentoGuardar = true;
    this.formulario.markAllAsTouched();
    if (this.formulario.invalid || this.errorAtributos) {
      this.avisos.error('Revise el formulario', 'Hay campos con errores.');
      return;
    }

    if (this.esNuevo) {
      this.crear();
      return;
    }

    this.guardando = true;
    this.adminCatalogo.actualizarProducto(this.productoId!, this.peticionProducto()).subscribe({
      next: producto => {
        this.producto = producto;
        this.formulario.patchValue({ slug: producto.slug });
        this.formulario.markAsPristine();
        this.guardando = false;
        this.avisos.exito('Producto guardado', producto.nombre);
      },
      error: error => {
        this.guardando = false;
        this.avisos.error('No se pudo guardar', mensajeDeError(error));
      }
    });
  }

  private crear(): void {
    if (this.filas.length === 0) {
      this.avisos.error('Falta una variante', 'El producto necesita al menos una variante (SKU).');
      return;
    }
    const conError = this.filas.find(f => this.errorFila(f));
    if (conError) {
      this.avisos.error('Revise las variantes', this.errorFila(conError)!);
      return;
    }

    this.guardando = true;
    this.adminCatalogo.crearProducto({ ...this.peticionProducto(), variantes: this.filas.map(f => this.peticionVariante(f)) }).subscribe({
      next: producto => {
        this.avisos.exito('Producto creado', 'Ahora puede subir sus imágenes.');
        this.router.navigate(['/admin/catalogo', producto.id], { replaceUrl: true });
      },
      error: error => {
        this.guardando = false;
        this.avisos.error('No se pudo crear el producto', mensajeDeError(error));
      }
    });
  }

  cambiarEstadoProducto(): void {
    if (!this.producto) return;
    const activo = !this.producto.activo;
    this.cambiandoEstado = true;
    this.adminCatalogo.cambiarEstadoProducto(this.producto.id, activo).subscribe({
      next: () => {
        this.producto!.activo = activo;
        this.cambiandoEstado = false;
        this.avisos.exito(activo ? 'Producto activado' : 'Producto desactivado', activo ? 'Ya se ve en la tienda.' : 'Ya no se muestra en la tienda.');
      },
      error: error => {
        this.cambiandoEstado = false;
        this.avisos.error('No se pudo cambiar el estado', mensajeDeError(error));
      }
    });
  }

  // ---------- Imágenes ----------
  get imagenes(): ImagenProducto[] {
    return [...(this.producto?.imagenes ?? [])].sort((a, b) => Number(b.esPrincipal) - Number(a.esPrincipal) || a.orden - b.orden);
  }

  seleccionarArchivo(evento: Event): void {
    const archivo = (evento.target as HTMLInputElement).files?.[0] ?? null;
    this.errorImagen = null;
    this.archivo = null;
    if (!archivo) return;

    if (!TIPOS_IMAGEN.includes(archivo.type)) {
      this.errorImagen = 'Solo se aceptan imágenes JPG, PNG o WEBP.';
    } else if (archivo.size > TAMANO_MAXIMO_IMAGEN_MB * 1024 * 1024) {
      this.errorImagen = `La imagen debe pesar como máximo ${TAMANO_MAXIMO_IMAGEN_MB} MB.`;
    } else {
      this.archivo = archivo;
    }
  }

  subirImagen(campo: HTMLInputElement): void {
    if (!this.producto || !this.archivo) return;

    this.subiendo = true;
    const esPrincipal = this.producto.imagenes.length === 0;
    this.adminCatalogo.subirImagen(this.producto.id, this.archivo, esPrincipal, this.textoAlternativo || this.producto.nombre).subscribe({
      next: () => {
        campo.value = '';
        this.archivo = null;
        this.textoAlternativo = '';
        this.subiendo = false;
        this.avisos.exito('Imagen subida');
        this.refrescarImagenes();
      },
      error: error => {
        this.subiendo = false;
        this.errorImagen = mensajeDeError(error, 'No se pudo subir la imagen.');
      }
    });
  }

  marcarPrincipal(imagen: ImagenProducto): void {
    this.ocupadas.add(imagen.id);
    this.adminCatalogo.marcarImagenPrincipal(imagen.id).subscribe({
      next: () => this.refrescarImagenes(),
      error: error => {
        this.ocupadas.delete(imagen.id);
        this.avisos.error('No se pudo marcar como principal', mensajeDeError(error));
      }
    });
  }

  eliminarImagen(imagen: ImagenProducto): void {
    if (!confirm('¿Eliminar esta imagen? El archivo se borra del servidor.')) return;
    this.ocupadas.add(imagen.id);
    this.adminCatalogo.eliminarImagen(imagen.id).subscribe({
      next: () => {
        this.avisos.exito('Imagen eliminada');
        this.refrescarImagenes();
      },
      error: error => {
        this.ocupadas.delete(imagen.id);
        this.avisos.error('No se pudo eliminar la imagen', mensajeDeError(error));
      }
    });
  }

  private refrescarImagenes(): void {
    this.adminCatalogo.obtenerProducto(this.productoId!).subscribe(producto => {
      this.producto!.imagenes = producto.imagenes;
      this.ocupadas.clear();
    });
  }

  // ---------- Conversión entre formulario y API ----------
  private cargarProducto(producto: ProductoDetalle): void {
    this.producto = producto;
    this.formulario.setValue({
      nombre: producto.nombre,
      slug: producto.slug,
      categoriaId: producto.categoriaId,
      marcaId: producto.marcaId ?? '',
      descripcionCorta: producto.descripcionCorta ?? '',
      descripcion: producto.descripcion ?? '',
      precioBase: producto.precioBase,
      destacado: producto.destacado
    });
    this.atributos = Object.entries(producto.atributos ?? {}).map(([clave, valor]) => ({ clave, valor }));
    this.nombresOpciones = [...new Set(producto.variantes.flatMap(v => Object.keys(v.opciones ?? {})))];
    this.filas = producto.variantes.map(v => this.filaDesde(v));
  }

  private peticionProducto(): ProductoPeticion {
    const valor = this.formulario.getRawValue();
    return {
      nombre: valor.nombre.trim(),
      slug: valor.slug.trim() || null,
      categoriaId: valor.categoriaId,
      marcaId: valor.marcaId || null,
      descripcionCorta: valor.descripcionCorta.trim() || null,
      descripcion: valor.descripcion.trim() || null,
      precioBase: valor.precioBase,
      destacado: valor.destacado,
      atributos: Object.fromEntries(this.atributos.filter(a => a.clave.trim()).map(a => [a.clave.trim(), a.valor.trim()]))
    };
  }

  private peticionVariante(fila: FilaVariante): VariantePeticion {
    const opciones = Object.fromEntries(
      this.nombresOpciones.map(o => [o, (fila.opciones[o] ?? '').trim()]).filter(([, valor]) => valor)
    );
    return { sku: fila.sku.trim().toUpperCase(), opciones, precio: fila.precio ?? null, precioAntes: fila.precioAntes ?? null };
  }

  private filaNueva(): FilaVariante {
    return { id: null, sku: '', opciones: {}, precio: null, precioAntes: null, precioEfectivo: null, activa: true, editando: true, guardando: false, respaldo: null };
  }

  private filaDesde(variante: Variante): FilaVariante {
    return {
      id: variante.id,
      sku: variante.sku,
      opciones: { ...(variante.opciones ?? {}) },
      precio: variante.precioPropio,
      precioAntes: variante.precioAntes,
      precioEfectivo: variante.precio,
      activa: variante.activa,
      editando: false,
      guardando: false,
      respaldo: null
    };
  }
}
