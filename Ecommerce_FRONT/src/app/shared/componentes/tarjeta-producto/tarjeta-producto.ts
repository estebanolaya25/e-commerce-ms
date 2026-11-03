import { Component, Input } from '@angular/core';
import { ProductoResumen } from '../../../core/modelos/catalogo.model';

@Component({
  selector: 'app-tarjeta-producto',
  standalone: false,
  templateUrl: './tarjeta-producto.html',
  styleUrl: './tarjeta-producto.scss'
})
export class TarjetaProducto {
  @Input({ required: true }) producto!: ProductoResumen;

  get descuento(): number | null {
    const { precioAntes, precioDesde } = this.producto;
    return precioAntes && precioAntes > precioDesde ? Math.round((1 - precioDesde / precioAntes) * 100) : null;
  }
}
