import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, map } from 'rxjs';
import { Pagina } from '../../../../core/modelos/catalogo.model';
import { ESTADOS_PAGO, EstadoPago, Pago } from '../../../../core/modelos/pago.model';
import { AdminPagosService } from '../../../../core/servicios/admin-pagos.service';
import { AdminPedidosService } from '../../../../core/servicios/admin-pedidos.service';
import { AvisosService } from '../../../../core/servicios/avisos.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';
import { fechaUtc } from '../../../../core/utilidades/fechas';

const TAMANO_PAGINA = 20;
const PATRON_GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const ESTADOS: EstadoPago[] = ['Aprobado', 'Rechazado', 'Procesando', 'Reembolsado'];

@Component({
  selector: 'app-pagos',
  standalone: false,
  templateUrl: './pagos.html',
  styleUrl: './pagos.scss'
})
export class AdminPagos implements OnInit {
  resultado: Pagina<Pago> | null = null;
  cargando = true;
  error: string | null = null;
  estado: EstadoPago | '' = '';
  pedidoId = '';
  pedidoNumero = '';
  pagina = 1;
  buscando = false;
  totales: Partial<Record<string, number>> = {};

  readonly estados = ESTADOS.map(valor => ({ valor, ...ESTADOS_PAGO[valor] }));

  constructor(
    private adminPagos: AdminPagosService,
    private adminPedidos: AdminPedidosService,
    private avisos: AvisosService,
    private ruta: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.cargarTotales();
    this.ruta.queryParamMap.subscribe(parametros => {
      this.estado = (parametros.get('estado') as EstadoPago | null) ?? '';
      this.pedidoId = parametros.get('pedidoId') ?? '';
      this.pedidoNumero = parametros.get('numero') ?? '';
      this.pagina = Math.max(1, Number(parametros.get('pagina')) || 1);
      this.cargar();
    });
  }

  // Aprobados sobre los pagos ya decididos (sin contar los que siguen procesándose).
  get tasaAprobacion(): number | null {
    const decididos = (this.totales['Aprobado'] ?? 0) + (this.totales['Rechazado'] ?? 0) + (this.totales['Reembolsado'] ?? 0);
    return decididos ? ((this.totales['Aprobado'] ?? 0) + (this.totales['Reembolsado'] ?? 0)) / decididos : null;
  }

  filtrarEstado(estado: EstadoPago | ''): void {
    this.navegar({ estado: estado || null, pagina: null });
  }

  // Acepta el número del pedido (PED-2026-000042) o su id. Pagos solo filtra por id,
  // así que el número se traduce consultando Pedidos.
  buscarPedido(texto: string): void {
    const valor = texto.trim();
    if (!valor) {
      this.navegar({ pedidoId: null, numero: null, pagina: null });
      return;
    }
    if (PATRON_GUID.test(valor)) {
      this.navegar({ pedidoId: valor.toLowerCase(), numero: null, pagina: null });
      return;
    }

    this.buscando = true;
    this.adminPedidos.listar({ pagina: 1, tamano: 1, numero: valor }).subscribe({
      next: pagina => {
        this.buscando = false;
        const pedido = pagina.items[0];
        if (!pedido) {
          this.avisos.error('Pedido no encontrado', `No hay ningún pedido con el número ${valor.toUpperCase()}.`);
          return;
        }
        this.navegar({ pedidoId: pedido.pedidoId, numero: pedido.numero, pagina: null });
      },
      error: error => {
        this.buscando = false;
        this.avisos.error('No se pudo buscar el pedido', mensajeDeError(error));
      }
    });
  }

  limpiar(): void {
    this.router.navigate([], { relativeTo: this.ruta, queryParams: {} });
  }

  irAPagina(pagina: number): void {
    this.navegar({ pagina });
  }

  fecha(texto: string): Date {
    return fechaUtc(texto);
  }

  // Cuánto tardó la pasarela simulada en responder (entre 2 y 8 segundos).
  demora(pago: Pago): string | null {
    if (!pago.procesadoEn) return null;
    const segundos = (fechaUtc(pago.procesadoEn).getTime() - fechaUtc(pago.createdAt).getTime()) / 1000;
    return `${segundos.toLocaleString('es-CO', { maximumFractionDigits: 1 })} s`;
  }

  private navegar(cambios: Record<string, string | number | null>): void {
    this.router.navigate([], { relativeTo: this.ruta, queryParams: cambios, queryParamsHandling: 'merge' });
  }

  private cargarTotales(): void {
    const peticiones = Object.fromEntries(
      ['', ...ESTADOS].map(estado => [estado || 'Todos', this.adminPagos.listar({ pagina: 1, tamano: 1, estado: (estado || undefined) as EstadoPago | undefined }).pipe(map(p => p.total))])
    );
    forkJoin(peticiones).subscribe({ next: totales => (this.totales = totales) });
  }

  private cargar(): void {
    this.cargando = true;
    this.error = null;
    this.adminPagos
      .listar({ pagina: this.pagina, tamano: TAMANO_PAGINA, estado: this.estado || undefined, pedidoId: this.pedidoId || undefined })
      .subscribe({
        next: resultado => {
          this.resultado = resultado;
          this.cargando = false;
        },
        error: error => {
          this.error = mensajeDeError(error, 'No se pudieron cargar los pagos.');
          this.cargando = false;
        }
      });
  }
}
