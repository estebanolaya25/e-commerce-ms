import { NgModule } from '@angular/core';
import { SharedModule } from '../../shared/shared-module';
import { CarritoRoutingModule } from './carrito-routing-module';
import { Carrito } from './paginas/carrito/carrito';

@NgModule({
  declarations: [Carrito],
  imports: [SharedModule, CarritoRoutingModule]
})
export class CarritoModule {}
