import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { Carrito } from './paginas/carrito/carrito';

const routes: Routes = [
  { path: '', component: Carrito, title: 'Carrito · Tienda Andina' }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class CarritoRoutingModule {}
