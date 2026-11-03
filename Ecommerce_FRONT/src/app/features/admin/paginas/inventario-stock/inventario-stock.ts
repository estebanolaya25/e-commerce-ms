import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, forkJoin, map, of } from 'rxjs';
import { Pagina, VarianteResumen } from '../../../../core/modelos/catalogo.model';
import { StockSku, VistaStock } from '../../../../core/modelos/inventario.model';
import { AdminInventarioService } from '../../../../core/servicios/admin-inventario.service';
import { CatalogoService } from '../../../../core/servicios/catalogo.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';
import { fechaUtc } from '../../../../core/utilidades/fechas';

const TAMANO_PAGINA = 20;

@Component({
  selector: 'app-inventario-stock',
  standalone: false,
  templateUrl: './inventario-stock.html',
  styleUrl: './inventario-stock.scss'
})
export class InventarioStock implements OnInit {
  resultado: Pagina<StockSku> | null = null;
  variantes = new Map<string, VarianteResumen>();
  cargando = true;
  error: string | null = null;
  sku = '';
  vista: VistaStock = 'todos';
  pagina = 1;
  totales: Record<VistaStock, number | null> = { todos: null, 'bajo-minimo': null, agotados: null };

  readonly vistas: { valor: VistaStock; texto: string; icono: string }[] = [
    { valor: 'todos', texto: 'Todos', icono: 'bi-boxes' },
    { valor: 'bajo-minimo', texto: 'Bajo mínimo', icono: 'bi-exclamation-triangle' },
    { valor: 'agotados', texto: 'Agotados', icono: 'bi-x-octagon' }
  ];

  constructor(
    private adminInventario: AdminInventarioService,
    private catalogo: CatalogoService,
    private ruta: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.cargarTotales();
    this.ruta.queryParamMap.subscribe(parametros => {
      const vista = parametros.get('vista') as VistaStock | null;
      this.vista = vista && vista in this.totales ? vista : 'todos';
      this.sku = parametros.get('sku') ?? '';
      this.pagina = Math.max(1, Number(parametros.get('pagina')) || 1);
      this.cargar();
    });
  }

  cambiarVista(vista: VistaStock): void {
    this.navegar({ vista: vista === 'todos' ? null : vista, pagina: null });
  }

  buscarSku(texto: string): void {
    this.navegar({ sku: texto.trim().toUpperCase() || null, pagina: null });
  }

  irAPagina(pagina: number): void {
    this.navegar({ pagina });
  }

  estado(stock: StockSku): { texto: string; clase: string } {
    if (stock.cantidadDisponible <= 0) return { texto: 'Agotado', clase: 'agotado' };
    if (stock.bajoMinimo) return { texto: 'Bajo mínimo', clase: 'bajo' };
    return { texto: 'Normal', clase: 'normal' };
  }

  fecha(texto: string): Date {
    return fechaUtc(texto);
  }

  private navegar(cambios: Record<string, string | number | null>): void {
    this.router.navigate([], { relativeTo: this.ruta, queryParams: cambios, queryParamsHandling: 'merge' });
  }

  private cargarTotales(): void {
    const total = (vista: VistaStock) => this.adminInventario.listar({ pagina: 1, tamano: 1, vista }).pipe(map(p => p.total));
    forkJoin({ todos: total('todos'), 'bajo-minimo': total('bajo-minimo'), agotados: total('agotados') }).subscribe({
      next: totales => (this.totales = totales)
    });
  }

  private cargar(): void {
    this.cargando = true;
    this.error = null;
    this.adminInventario.listar({ pagina: this.pagina, tamano: TAMANO_PAGINA, sku: this.sku || undefined, vista: this.vista }).subscribe({
      next: resultado => {
        this.resultado = resultado;
        this.cargando = false;
        this.cargarNombres(resultado.items.map(i => i.sku));
      },
      error: error => {
        this.error = mensajeDeError(error, 'No se pudo cargar el inventario.');
        this.cargando = false;
      }
    });
  }

  // Nombres e imágenes desde Catálogo. Si falla, la tabla sigue funcionando con el SKU.
  private cargarNombres(skus: string[]): void {
    if (skus.length === 0) return;
    this.catalogo
      .obtenerVariantesPorSku(skus)
      .pipe(catchError(() => of([] as VarianteResumen[])))
      .subscribe(variantes => (this.variantes = new Map(variantes.map(v => [v.sku, v]))));
  }
}
