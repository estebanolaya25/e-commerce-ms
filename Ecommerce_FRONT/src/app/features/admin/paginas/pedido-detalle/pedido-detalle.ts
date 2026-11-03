import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { catchError, of } from 'rxjs';
import { ROLES_DESPACHO, ROLES_PAGOS, esSoloBodega } from '../../../../core/config/roles';
import { Pago } from '../../../../core/modelos/pago.model';
import { ESTADOS_PEDIDO, EstadoPedido, Pedido } from '../../../../core/modelos/pedido.model';
import { AdminPagosService } from '../../../../core/servicios/admin-pagos.service';
import { AdminPedidosService } from '../../../../core/servicios/admin-pedidos.service';
import { AvisosService } from '../../../../core/servicios/avisos.service';
import { SesionService } from '../../../../core/servicios/sesion.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';
import { fechaUtc } from '../../../../core/utilidades/fechas';

// Único camino de despacho que acepta Pedidos: cada estado solo puede avanzar al siguiente.
const SIGUIENTE: Partial<Record<EstadoPedido, { estado: EstadoPedido; accion: string; ayuda: string }>> = {
  Pagado: { estado: 'EnPreparacion', accion: 'Pasar a preparación', ayuda: 'Bodega empieza a alistar los productos.' },
  EnPreparacion: { estado: 'Enviado', accion: 'Marcar como enviado', ayuda: 'Escriba la guía de la transportadora para el cliente.' },
  Enviado: { estado: 'Entregado', accion: 'Marcar como entregado', ayuda: 'El cliente recibió el pedido.' }
};

const ETAPAS: EstadoPedido[] = ['Pagado', 'EnPreparacion', 'Enviado', 'Entregado'];

@Component({
  selector: 'app-admin-pedido-detalle',
  standalone: false,
  templateUrl: './pedido-detalle.html',
  styleUrl: './pedido-detalle.scss'
})
export class AdminPedidoDetalle implements OnInit {
  pedido: Pedido | null = null;
  pagos: Pago[] | null = null;
  cargando = true;
  noEncontrado = false;
  motivo = '';
  avanzando = false;
  readonly puedeAvanzar: boolean;
  readonly vePagos: boolean;
  readonly soloBodega: boolean;
  readonly etapas = ETAPAS.map(estado => ({ estado, ...ESTADOS_PEDIDO[estado] }));

  constructor(
    private ruta: ActivatedRoute,
    private adminPedidos: AdminPedidosService,
    private adminPagos: AdminPagosService,
    private avisos: AvisosService,
    sesion: SesionService
  ) {
    // Support solo consulta; despachar es de Administrator y Warehouse (el backend también lo exige).
    this.puedeAvanzar = sesion.tieneAlgunRol(ROLES_DESPACHO);
    this.vePagos = sesion.tieneAlgunRol(ROLES_PAGOS);
    this.soloBodega = esSoloBodega(sesion);
  }

  ngOnInit(): void {
    this.adminPedidos.obtener(this.ruta.snapshot.paramMap.get('pedidoId')!).subscribe({
      next: pedido => {
        this.pedido = pedido;
        this.cargando = false;
        if (this.vePagos) this.cargarPagos(pedido.pedidoId);
      },
      error: () => {
        this.noEncontrado = true;
        this.cargando = false;
      }
    });
  }

  // Si Pagos no responde, el detalle del pedido se sigue mostrando sin esta sección.
  private cargarPagos(pedidoId: string): void {
    this.adminPagos
      .listar({ pagina: 1, tamano: 20, pedidoId })
      .pipe(catchError(() => of(null)))
      .subscribe(pagina => (this.pagos = pagina?.items ?? null));
  }

  get siguiente() {
    return this.pedido ? SIGUIENTE[this.pedido.estado] ?? null : null;
  }

  get etapaActual(): number {
    return this.pedido ? ETAPAS.indexOf(this.pedido.estado) : -1;
  }

  fecha(texto: string): Date {
    return fechaUtc(texto);
  }

  opcionesTexto(opciones: string | null): string {
    if (!opciones) return '';
    try {
      return Object.values(JSON.parse(opciones) as Record<string, string>).join(' · ');
    } catch {
      return '';
    }
  }

  avanzar(): void {
    const siguiente = this.siguiente;
    if (!this.pedido || !siguiente) return;

    this.avanzando = true;
    this.adminPedidos.avanzar(this.pedido.pedidoId, siguiente.estado, this.motivo).subscribe({
      next: pedido => {
        this.pedido = pedido;
        this.motivo = '';
        this.avanzando = false;
        this.avisos.exito('Pedido actualizado', `${pedido.numero} ahora está "${ESTADOS_PEDIDO[pedido.estado].texto}".`);
      },
      error: error => {
        this.avanzando = false;
        this.avisos.error('No se pudo actualizar', mensajeDeError(error));
      }
    });
  }
}
