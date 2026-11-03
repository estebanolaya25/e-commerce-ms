import { Component, Input } from '@angular/core';
import { ESTADOS_PAGO, EstadoPago } from '../../../core/modelos/pago.model';

@Component({
  selector: 'app-estado-pago',
  standalone: false,
  templateUrl: './estado-pago.html',
  styleUrl: './estado-pago.scss'
})
export class EstadoPagoInsignia {
  @Input({ required: true }) estado!: EstadoPago;

  get info() {
    return ESTADOS_PAGO[this.estado] ?? { texto: this.estado, icono: 'bi-dot', clase: 'neutro' };
  }
}
