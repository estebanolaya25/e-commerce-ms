import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, map, of, switchMap, tap } from 'rxjs';
import { ImagenProducto, ProductoDetalle, Variante } from '../../../../core/modelos/catalogo.model';
import { Disponibilidad } from '../../../../core/modelos/inventario.model';
import { AvisosService } from '../../../../core/servicios/avisos.service';
import { CarritoService } from '../../../../core/servicios/carrito.service';
import { CatalogoService } from '../../../../core/servicios/catalogo.service';
import { InventarioService } from '../../../../core/servicios/inventario.service';
import { SesionService } from '../../../../core/servicios/sesion.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';

const MAXIMO_POR_PRODUCTO = 10;

@Component({
  selector: 'app-detalle',
  standalone: false,
  templateUrl: './detalle.html',
  styleUrl: './detalle.scss'
})
export class Detalle implements OnInit {
  private readonly destroyRef = inject(DestroyRef);

  producto: ProductoDetalle | null = null;
  cargando = true;
  noEncontrado = false;

  // Opciones del producto (por ejemplo color y talla) y lo que el cliente eligió.
  claves: string[] = [];
  valores: Record<string, string[]> = {};
  seleccion: Record<string, string> = {};

  // Stock por SKU, desde Inventario. null = Inventario no respondió.
  stock: Record<string, Disponibilidad> | null = null;
  imagenActual: ImagenProducto | null = null;
  cantidad = 1;
  agregando = false;

  constructor(
    private ruta: ActivatedRoute,
    private catalogoService: CatalogoService,
    private inventarioService: InventarioService,
    private titulo: Title,
    private carritoService: CarritoService,
    private sesion: SesionService,
    private avisos: AvisosService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.ruta.paramMap
      .pipe(
        map(parametros => parametros.get('slug')!),
        tap(() => this.reiniciar()),
        switchMap(slug => this.catalogoService.obtenerProducto(slug).pipe(catchError(() => of(null)))),
        tap(producto => this.mostrarProducto(producto)),
        switchMap(producto =>
          producto
            ? this.inventarioService.obtenerDisponibilidad(producto.variantes.map(v => v.sku)).pipe(catchError(() => of(null)))
            : of(null)
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(disponibilidad => {
        if (disponibilidad) {
          this.stock = Object.fromEntries(disponibilidad.map(d => [d.sku, d]));
          this.preferirVarianteConStock();
        }
      });
  }

  get variante(): Variante | null {
    return this.producto?.variantes.find(v => this.claves.every(c => v.opciones[c] === this.seleccion[c])) ?? null;
  }

  get stockVariante(): Disponibilidad | null {
    const variante = this.variante;
    return variante && this.stock ? this.stock[variante.sku] ?? null : null;
  }

  get descuento(): number | null {
    const variante = this.variante;
    return variante?.precioAntes && variante.precioAntes > variante.precio
      ? Math.round((1 - variante.precio / variante.precioAntes) * 100)
      : null;
  }

  get maximoCantidad(): number {
    const stock = this.stockVariante;
    return stock ? Math.min(MAXIMO_POR_PRODUCTO, stock.disponible) : MAXIMO_POR_PRODUCTO;
  }

  get atributos(): [string, string][] {
    return Object.entries(this.producto?.atributos ?? {});
  }

  etiqueta(clave: string): string {
    const nombres: Record<string, string> = { color: 'Color', talla: 'Talla', almacenamiento: 'Almacenamiento', memoria: 'Memoria', tamano: 'Tamaño' };
    return nombres[clave] ?? clave.charAt(0).toUpperCase() + clave.slice(1);
  }

  // ¿Existe una variante con este valor y lo demás ya elegido? Si no, el botón se ve atenuado.
  combinable(clave: string, valor: string): boolean {
    return this.buscarVariante({ ...this.seleccion, [clave]: valor }) !== null;
  }

  // ¿La variante que resultaría de elegir este valor está agotada?
  agotado(clave: string, valor: string): boolean {
    const variante = this.buscarVariante({ ...this.seleccion, [clave]: valor });
    return !!(variante && this.stock?.[variante.sku]?.agotado);
  }

  elegir(clave: string, valor: string): void {
    const deseada = { ...this.seleccion, [clave]: valor };
    // Si la combinación no existe (p. ej. talla S en un color que no la tiene), se ajusta lo demás.
    const variante = this.buscarVariante(deseada) ?? this.producto!.variantes.find(v => v.opciones[clave] === valor)!;
    this.seleccion = { ...variante.opciones };
    this.cantidad = Math.min(this.cantidad, Math.max(1, this.maximoCantidad));
    this.actualizarImagen();
  }

  agregarAlCarrito(): void {
    const variante = this.variante;
    if (!variante || !this.producto) {
      return;
    }
    if (!this.sesion.estaAutenticada) {
      this.router.navigate(['/cuenta/ingresar'], { queryParams: { volver: this.router.url } });
      return;
    }

    this.agregando = true;
    this.carritoService.agregar(variante.sku, this.cantidad).subscribe({
      next: () => {
        this.agregando = false;
        const detalle = Object.values(variante.opciones).join(' · ');
        this.avisos.exito('Agregado al carrito', `${this.cantidad} × ${this.producto!.nombre}${detalle ? ' (' + detalle + ')' : ''}`, {
          texto: 'Ver carrito',
          ruta: '/carrito'
        });
      },
      error: error => {
        this.agregando = false;
        this.avisos.error('No se pudo agregar', mensajeDeError(error));
      }
    });
  }

  cambiarCantidad(delta: number): void {
    this.cantidad = Math.min(Math.max(1, this.cantidad + delta), Math.max(1, this.maximoCantidad));
  }

  private reiniciar(): void {
    this.cargando = true;
    this.noEncontrado = false;
    this.producto = null;
    this.stock = null;
    this.cantidad = 1;
  }

  private mostrarProducto(producto: ProductoDetalle | null): void {
    this.cargando = false;
    if (!producto) {
      this.noEncontrado = true;
      this.titulo.setTitle('Producto no encontrado · Tienda Andina');
      return;
    }

    this.producto = producto;
    this.titulo.setTitle(`${producto.nombre} · Tienda Andina`);
    this.claves = Object.keys(producto.variantes[0]?.opciones ?? {});
    this.valores = Object.fromEntries(
      this.claves.map(clave => [clave, [...new Set(producto.variantes.map(v => v.opciones[clave]))]])
    );
    this.seleccion = { ...(producto.variantes[0]?.opciones ?? {}) };
    this.actualizarImagen();
  }

  private preferirVarianteConStock(): void {
    if (this.stockVariante?.agotado) {
      const conStock = this.producto?.variantes.find(v => !this.stock?.[v.sku]?.agotado);
      if (conStock) {
        this.seleccion = { ...conStock.opciones };
        this.actualizarImagen();
      }
    }
  }

  private buscarVariante(opciones: Record<string, string>): Variante | null {
    return this.producto?.variantes.find(v => this.claves.every(c => v.opciones[c] === opciones[c])) ?? null;
  }

  // Muestra la foto del color elegido; si no hay, la principal.
  private actualizarImagen(): void {
    const producto = this.producto;
    if (!producto || producto.imagenes.length === 0) {
      this.imagenActual = null;
      return;
    }
    const color = this.seleccion['color'];
    const delColor = color
      ? producto.imagenes.find(i => producto.variantes.some(v => v.id === i.varianteId && v.opciones['color'] === color))
      : undefined;
    this.imagenActual = delColor ?? producto.imagenes.find(i => i.esPrincipal) ?? producto.imagenes[0];
  }
}
