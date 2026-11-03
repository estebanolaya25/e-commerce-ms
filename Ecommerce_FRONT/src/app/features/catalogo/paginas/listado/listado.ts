import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { forkJoin, switchMap, tap } from 'rxjs';
import {
  Categoria,
  FiltroProductos,
  Marca,
  OrdenProductos,
  Pagina,
  ProductoResumen
} from '../../../../core/modelos/catalogo.model';
import { CatalogoService } from '../../../../core/servicios/catalogo.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';

const TAMANO_PAGINA = 12;

@Component({
  selector: 'app-listado',
  standalone: false,
  templateUrl: './listado.html',
  styleUrl: './listado.scss'
})
export class Listado implements OnInit {
  private readonly destroyRef = inject(DestroyRef);

  categorias: Categoria[] = [];
  marcas: Marca[] = [];
  resultado: Pagina<ProductoResumen> | null = null;
  filtro: FiltroProductos = { pagina: 1, tamano: TAMANO_PAGINA, orden: 'recientes' };
  cargando = true;
  error: string | null = null;

  precioMin: number | null = null;
  precioMax: number | null = null;

  readonly opcionesOrden: { valor: OrdenProductos; texto: string }[] = [
    { valor: 'recientes', texto: 'Más recientes' },
    { valor: 'precio_asc', texto: 'Menor precio' },
    { valor: 'precio_desc', texto: 'Mayor precio' },
    { valor: 'nombre', texto: 'Nombre A-Z' },
    { valor: 'relevancia', texto: 'Más relevantes' }
  ];

  constructor(private catalogoService: CatalogoService, private ruta: ActivatedRoute, private router: Router) {}

  ngOnInit(): void {
    forkJoin([this.catalogoService.obtenerCategorias(), this.catalogoService.obtenerMarcas()]).subscribe(
      ([categorias, marcas]) => {
        this.categorias = categorias;
        this.marcas = marcas;
      }
    );

    // La URL es la fuente de verdad: cada cambio de filtro navega y esto recarga los productos.
    this.ruta.queryParamMap
      .pipe(
        tap(parametros => {
          this.filtro = this.leerFiltro(parametros);
          this.precioMin = this.filtro.precioMin ?? null;
          this.precioMax = this.filtro.precioMax ?? null;
          this.cargando = true;
          this.error = null;
        }),
        switchMap(() => this.catalogoService.listarProductos(this.filtro)),
        takeUntilDestroyed(this.destroyRef)
      )
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

  get titulo(): string {
    if (this.filtro.buscar) return `Resultados para "${this.filtro.buscar}"`;
    if (this.filtro.categoria) return this.nombreCategoria(this.filtro.categoria);
    if (this.filtro.soloDestacados) return 'Productos destacados';
    return 'Todos los productos';
  }

  get hayFiltros(): boolean {
    const f = this.filtro;
    return !!(f.categoria || f.marca || f.buscar || f.precioMin != null || f.precioMax != null || f.soloDestacados);
  }

  aplicar(cambios: Record<string, string | number | boolean | null>): void {
    this.router.navigate([], {
      relativeTo: this.ruta,
      queryParams: { ...cambios, pagina: null },
      queryParamsHandling: 'merge'
    });
  }

  aplicarPrecio(): void {
    this.aplicar({ precioMin: this.precioMin || null, precioMax: this.precioMax || null });
  }

  irAPagina(pagina: number): void {
    this.router.navigate([], { relativeTo: this.ruta, queryParams: { pagina }, queryParamsHandling: 'merge' });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  limpiar(): void {
    this.router.navigate([], { relativeTo: this.ruta, queryParams: {} });
  }

  nombreCategoria(slug: string): string {
    for (const categoria of this.categorias) {
      if (categoria.slug === slug) return categoria.nombre;
      const hija = categoria.hijas.find(h => h.slug === slug);
      if (hija) return `${hija.nombre} de ${categoria.nombre}`;
    }
    return 'Categoría';
  }

  nombreMarca(slug: string): string {
    return this.marcas.find(m => m.slug === slug)?.nombre ?? slug;
  }

  private leerFiltro(parametros: ParamMap): FiltroProductos {
    const numero = (clave: string) => {
      const valor = Number(parametros.get(clave));
      return parametros.has(clave) && !Number.isNaN(valor) ? valor : undefined;
    };
    const orden = parametros.get('orden') as OrdenProductos | null;

    return {
      pagina: Math.max(1, numero('pagina') ?? 1),
      tamano: TAMANO_PAGINA,
      categoria: parametros.get('categoria') ?? undefined,
      marca: parametros.get('marca') ?? undefined,
      buscar: parametros.get('buscar') ?? undefined,
      precioMin: numero('precioMin'),
      precioMax: numero('precioMax'),
      soloDestacados: parametros.get('soloDestacados') === 'true',
      orden: this.opcionesOrden.some(o => o.valor === orden) ? orden! : 'recientes'
    };
  }
}
