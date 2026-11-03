import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { Checkout } from './paginas/checkout/checkout';
import { Confirmacion } from './paginas/confirmacion/confirmacion';
import { Pago } from './paginas/pago/pago';

const routes: Routes = [
  { path: '', component: Checkout, title: 'Datos de envío · Tienda Andina' },
  { path: 'pago/:pedidoId', component: Pago, title: 'Pago · Tienda Andina' },
  { path: 'confirmacion/:pedidoId', component: Confirmacion, title: 'Pedido confirmado · Tienda Andina' }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class CheckoutRoutingModule {}
