import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { DetallePedido } from './paginas/detalle-pedido/detalle-pedido';
import { MisPedidos } from './paginas/mis-pedidos/mis-pedidos';

const routes: Routes = [
  { path: '', component: MisPedidos, title: 'Mis pedidos · Tienda Andina' },
  { path: ':pedidoId', component: DetallePedido, title: 'Detalle del pedido · Tienda Andina' }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class PedidosRoutingModule {}
