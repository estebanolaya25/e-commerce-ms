import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SharedModule } from '../../shared/shared-module';
import { DetallePedido } from './paginas/detalle-pedido/detalle-pedido';
import { MisPedidos } from './paginas/mis-pedidos/mis-pedidos';
import { PedidosRoutingModule } from './pedidos-routing-module';

@NgModule({
  declarations: [MisPedidos, DetallePedido],
  imports: [SharedModule, FormsModule, PedidosRoutingModule]
})
export class PedidosModule {}
