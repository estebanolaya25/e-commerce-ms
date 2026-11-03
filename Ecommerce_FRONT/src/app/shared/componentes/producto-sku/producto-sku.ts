import { Component, Input } from '@angular/core';
import { VarianteResumen } from '../../../core/modelos/catalogo.model';

// Miniatura, nombre y opciones de una variante del catálogo. Si el catálogo no la
// devuelve (variante o producto inactivo), muestra solo el SKU.
@Component({
  selector: 'app-producto-sku',
  standalone: false,
  templateUrl: './producto-sku.html',
  styleUrl: './producto-sku.scss'
})
export class ProductoSku {
  @Input({ required: true }) sku!: string;
  @Input() variante: VarianteResumen | null | undefined;
  @Input() grande = false;

  get opciones(): string {
    return Object.values(this.variante?.opciones ?? {}).join(' · ');
  }
}
