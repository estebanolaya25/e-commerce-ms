import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { Marca, Pagina, ProductoResumen } from '../../../../core/modelos/catalogo.model';
import { AdminCatalogoService } from '../../../../core/servicios/admin-catalogo.service';
import { AvisosService } from '../../../../core/servicios/avisos.service';
import { CategoriaPlana, aplanarCategorias } from '../../../../core/utilidades/categorias';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';

const TAMANO_PAGINA = 15;

@Component({
  selector: 'app-catalogo-productos',
  standalone: false,
  templateUrl: './catalogo-productos.html',
  styleUrl: './catalogo-productos.scss'
})
export class CatalogoProductos implements OnInit {
  resultado: Pagina<ProductoResumen> | null = null;
  cargando = true;
  error: string | null = null;
  buscar = '';
  categoria = '';
  marca = '';
  pagina = 1;
  categorias: CategoriaPlana[] = [];
  marcas: Marca[] = [];
  readonly cambiando = new Set<string>();

  constructor(
    private adminCatalogo: AdminCatalogoService,
    private avisos: AvisosService,
    private ruta: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    forkJoin({ categorias: this.adminCatalogo.listarCategorias(), marcas: this.adminCatalogo.listarMarcas() }).subscribe({
      next: ({ categorias, marcas }) => {
        this.categorias = aplanarCategorias(categorias);
        this.marcas = marcas;
      }
    });

    this.ruta.queryParamMap.subscribe(parametros => {
      this.buscar = parametros.get('buscar') ?? '';
      this.categoria = parametros.get('categoria') ?? '';
      this.marca = parametros.get('marca') ?? '';
      this.pagina = Math.max(1, Number(parametros.get('pagina')) || 1);
      this.cargar();
    });
  }

  get hayFiltros(): boolean {
    return !!(this.buscar || this.categoria || this.marca);
  }

  filtrar(cambios: Record<string, string>): void {
    const queryParams: Record<string, string | null> = { pagina: null };
    for (const [clave, valor] of Object.entries(cambios)) queryParams[clave] = valor.trim() || null;
    this.router.navigate([], { relativeTo: this.ruta, queryParams, queryParamsHandling: 'merge' });
  }

  limpiar(): void {
    this.router.navigate([], { relativeTo: this.ruta, queryParams: {} });
  }

  irAPagina(pagina: number): void {
    this.router.navigate([], { relativeTo: this.ruta, queryParams: { pagina }, queryParamsHandling: 'merge' });
  }

  sangria(nivel: number): string {
    return '\u00a0\u00a0'.repeat(nivel * 2) + (nivel ? '› ' : '');
  }

  cambiarEstado(producto: ProductoResumen, evento: Event): void {
    const casilla = evento.target as HTMLInputElement;
    const activo = casilla.checked;
    this.cambiando.add(producto.id);
    this.adminCatalogo.cambiarEstadoProducto(producto.id, activo).subscribe({
      next: () => {
        producto.activo = activo;
        this.cambiando.delete(producto.id);
        this.avisos.exito(activo ? 'Producto activado' : 'Producto desactivado', activo ? `${producto.nombre} ya se ve en la tienda.` : `${producto.nombre} ya no se muestra en la tienda.`);
      },
      error: error => {
        casilla.checked = producto.activo;
        this.cambiando.delete(producto.id);
        this.avisos.error('No se pudo cambiar el estado', mensajeDeError(error));
      }
    });
  }

  private cargar(): void {
    this.cargando = true;
    this.error = null;
    this.adminCatalogo
      .listarProductos({ pagina: this.pagina, tamano: TAMANO_PAGINA, buscar: this.buscar || undefined, categoria: this.categoria || undefined, marca: this.marca || undefined })
      .subscribe({
        next: resultado => {
          this.resultado = resultado;
          this.cargando = false;
        },
        error: error => {
          this.error = mensajeDeError(error, 'No se pudieron cargar los productos.');
          this.cargando = false;
        }
      });
  }
}
