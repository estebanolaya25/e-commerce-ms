import { Component, Input } from '@angular/core';
import { ESTADOS_PEDIDO, EstadoPedido } from '../../../core/modelos/pedido.model';

@Component({
  selector: 'app-estado-pedido',
  standalone: false,
  templateUrl: './estado-pedido.html',
  styleUrl: './estado-pedido.scss'
})
export class EstadoPedidoInsignia {
  @Input({ required: true }) estado!: EstadoPedido;

  get info() {
    return ESTADOS_PEDIDO[this.estado];
  }
}
