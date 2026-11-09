import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AsistenteChat } from './componentes/asistente-chat/asistente-chat';
import { Avisos } from './componentes/avisos/avisos';
import { BarraCarga } from './componentes/barra-carga/barra-carga';
import { EstadoPagoInsignia } from './componentes/estado-pago/estado-pago';
import { EstadoPedidoInsignia } from './componentes/estado-pedido/estado-pedido';
import { LineaTiempoPedido } from './componentes/linea-tiempo-pedido/linea-tiempo-pedido';
import { Navbar } from './componentes/navbar/navbar';
import { Paginador } from './componentes/paginador/paginador';
import { PiePagina } from './componentes/pie-pagina/pie-pagina';
import { ProductoSku } from './componentes/producto-sku/producto-sku';
import { TarjetaProducto } from './componentes/tarjeta-producto/tarjeta-producto';
import { NoEncontrada } from './paginas/no-encontrada/no-encontrada';
import { CopPipe } from './pipes/cop.pipe';

const COMPARTIDOS = [
  Navbar, PiePagina, NoEncontrada, TarjetaProducto, Paginador, Avisos, BarraCarga,
  EstadoPedidoInsignia, EstadoPagoInsignia, LineaTiempoPedido, ProductoSku, CopPipe, AsistenteChat
];

// Componentes, pipes y directivas que usan varios módulos. Se importa en cada módulo de funcionalidad.
@NgModule({
  declarations: COMPARTIDOS,
  imports: [CommonModule, RouterModule],
  exports: [CommonModule, RouterModule, ...COMPARTIDOS]
})
export class SharedModule {}
