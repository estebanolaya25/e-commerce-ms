import { Component, OnInit } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { ROLES_INVENTARIO, ROLES_PEDIDOS, esSoloBodega } from '../../../../core/config/roles';
import { ESTADOS_DESPACHABLES, ESTADOS_PEDIDO, EstadoPedido, PedidoResumen } from '../../../../core/modelos/pedido.model';
import { AdminInventarioService } from '../../../../core/servicios/admin-inventario.service';
import { AdminPedidosService } from '../../../../core/servicios/admin-pedidos.service';
import { SesionService } from '../../../../core/servicios/sesion.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';
import { fechaUtc } from '../../../../core/utilidades/fechas';

interface TarjetaEstado {
  estado: EstadoPedido;
  texto: string;
  icono: string;
  clase: string;
  total: number;
}

// Pedidos no tiene un endpoint de métricas: se pide el total de cada estado (página de 1 elemento).
const ESTADOS_RESUMEN: EstadoPedido[] = ['PendientePago', 'PagoEnProceso', 'Pagado', 'EnPreparacion', 'Enviado', 'Entregado', 'Cancelado', 'Expirado'];

@Component({
  selector: 'app-resumen',
  standalone: false,
  templateUrl: './resumen.html',
  styleUrl: './resumen.scss'
})
export class Resumen implements OnInit {
  readonly vePedidos: boolean;
  readonly veInventario: boolean;
  private readonly estadosResumen: EstadoPedido[];
  inventario: { bajoMinimo: number; agotados: number } | null = null;
  tarjetas: TarjetaEstado[] = [];
  ultimos: PedidoResumen[] = [];
  cargando = true;
  error: string | null = null;

  constructor(private adminPedidos: AdminPedidosService, private adminInventario: AdminInventarioService, private sesion: SesionService) {
    this.vePedidos = sesion.tieneAlgunRol(ROLES_PEDIDOS);
    this.veInventario = sesion.tieneAlgunRol(ROLES_INVENTARIO);
    this.estadosResumen = esSoloBodega(sesion) ? ESTADOS_DESPACHABLES : ESTADOS_RESUMEN;
  }

  ngOnInit(): void {
    if (this.veInventario) {
      const total = (vista: 'bajo-minimo' | 'agotados') => this.adminInventario.listar({ pagina: 1, tamano: 1, vista }).pipe(map(p => p.total));
      forkJoin({ bajoMinimo: total('bajo-minimo'), agotados: total('agotados') }).subscribe({ next: totales => (this.inventario = totales) });
    }

    if (!this.vePedidos) {
      this.cargando = false;
      return;
    }

    forkJoin({
      totales: forkJoin(this.estadosResumen.map(estado => this.adminPedidos.listar({ pagina: 1, tamano: 1, estado }))),
      ultimos: this.adminPedidos.listar({ pagina: 1, tamano: 6 })
    }).subscribe({
      next: ({ totales, ultimos }) => {
        this.tarjetas = this.estadosResumen.map((estado, i) => ({ estado, ...ESTADOS_PEDIDO[estado], total: totales[i].total }));
        this.ultimos = ultimos.items;
        this.cargando = false;
      },
      error: error => {
        this.error = mensajeDeError(error, 'No se pudo cargar el resumen.');
        this.cargando = false;
      }
    });
  }

  get porDespachar(): number {
    return this.tarjetas.filter(t => t.estado === 'Pagado' || t.estado === 'EnPreparacion').reduce((s, t) => s + t.total, 0);
  }

  get nombre(): string {
    return this.sesion.usuario?.nombre ?? '';
  }

  fecha(texto: string): Date {
    return fechaUtc(texto);
  }
}
