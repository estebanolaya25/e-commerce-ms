import { NgModule } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { SharedModule } from '../../shared/shared-module';
import { CheckoutRoutingModule } from './checkout-routing-module';
import { Pasos } from './componentes/pasos/pasos';
import { Checkout } from './paginas/checkout/checkout';
import { Confirmacion } from './paginas/confirmacion/confirmacion';
import { Pago } from './paginas/pago/pago';

@NgModule({
  declarations: [Pasos, Checkout, Pago, Confirmacion],
  imports: [SharedModule, ReactiveFormsModule, CheckoutRoutingModule]
})
export class CheckoutModule {}
