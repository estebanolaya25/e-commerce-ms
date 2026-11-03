import { NgModule } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { SharedModule } from '../../shared/shared-module';
import { AdminRoutingModule } from './admin-routing-module';
import { InsigniaRol } from './componentes/insignia-rol/insignia-rol';
import { PanelAdmin } from './componentes/panel-admin/panel-admin';
import { PestanasCatalogo } from './componentes/pestanas-catalogo/pestanas-catalogo';
import { CatalogoCategorias } from './paginas/catalogo-categorias/catalogo-categorias';
import { CatalogoMarcas } from './paginas/catalogo-marcas/catalogo-marcas';
import { CatalogoProductos } from './paginas/catalogo-productos/catalogo-productos';
import { InventarioDetalle } from './paginas/inventario-detalle/inventario-detalle';
import { InventarioStock } from './paginas/inventario-stock/inventario-stock';
import { AdminPedidoDetalle } from './paginas/pedido-detalle/pedido-detalle';
import { AdminPagos } from './paginas/pagos/pagos';
import { AdminPedidos } from './paginas/pedidos/pedidos';
import { ProductoFormulario } from './paginas/producto-formulario/producto-formulario';
import { Resumen } from './paginas/resumen/resumen';
import { UsuarioDetallePagina } from './paginas/usuario-detalle/usuario-detalle';
import { UsuarioNuevo } from './paginas/usuario-nuevo/usuario-nuevo';
import { AdminUsuarios } from './paginas/usuarios/usuarios';

@NgModule({
  declarations: [
    PanelAdmin, PestanasCatalogo, Resumen, AdminPedidos, AdminPedidoDetalle,
    CatalogoProductos, ProductoFormulario, CatalogoCategorias, CatalogoMarcas, InventarioStock, InventarioDetalle, AdminPagos,
    InsigniaRol, AdminUsuarios, UsuarioDetallePagina, UsuarioNuevo
  ],
  imports: [SharedModule, FormsModule, ReactiveFormsModule, AdminRoutingModule]
})
export class AdminModule {}
