import { Component, Input } from '@angular/core';
import { ESTADOS_PEDIDO, EstadoPedido, HistorialPedido } from '../../../core/modelos/pedido.model';
import { fechaUtc } from '../../../core/utilidades/fechas';

@Component({
  selector: 'app-linea-tiempo-pedido',
  standalone: false,
  templateUrl: './linea-tiempo-pedido.html',
  styleUrl: './linea-tiempo-pedido.scss'
})
export class LineaTiempoPedido {
  @Input({ required: true }) historial: HistorialPedido[] = [];

  // Del más reciente al más antiguo, como un timeline de seguimiento.
  get eventos(): HistorialPedido[] {
    return [...this.historial].reverse();
  }

  // Si el estado no cambió, es una nota (por ejemplo, el id del pago): se muestra como actualización.
  esNota(evento: HistorialPedido): boolean {
    return evento.estadoAnterior === evento.estadoNuevo;
  }

  info(estado: EstadoPedido) {
    return ESTADOS_PEDIDO[estado];
  }

  fecha(texto: string): Date {
    return fechaUtc(texto);
  }
}
