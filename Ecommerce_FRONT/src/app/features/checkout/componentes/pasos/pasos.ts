import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-pasos',
  standalone: false,
  templateUrl: './pasos.html',
  styleUrl: './pasos.scss'
})
export class Pasos {
  @Input() actual = 1;
  readonly pasos = ['Carrito', 'Envío', 'Pago', 'Confirmación'];
}
