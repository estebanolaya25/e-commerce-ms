import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ROLES_CATALOGO, ROLES_INVENTARIO, ROLES_PAGOS, ROLES_PEDIDOS, ROLES_USUARIOS } from '../../core/config/roles';
import { rolesGuard } from '../../core/guards/sesion.guards';
import { PanelAdmin } from './componentes/panel-admin/panel-admin';
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

const routes: Routes = [
  {
    path: '',
    component: PanelAdmin,
    children: [
      { path: '', component: Resumen, title: 'Panel · Tienda Andina' },
      { path: 'pedidos', component: AdminPedidos, canActivate: [rolesGuard(ROLES_PEDIDOS)], title: 'Pedidos · Panel' },
      { path: 'pedidos/:pedidoId', component: AdminPedidoDetalle, canActivate: [rolesGuard(ROLES_PEDIDOS)], title: 'Pedido · Panel' },
      {
        path: 'catalogo',
        canActivate: [rolesGuard(ROLES_CATALOGO)],
        children: [
          { path: '', component: CatalogoProductos, title: 'Catálogo · Panel' },
          { path: 'nuevo', component: ProductoFormulario, title: 'Nuevo producto · Panel' },
          { path: 'categorias', component: CatalogoCategorias, title: 'Categorías · Panel' },
          { path: 'marcas', component: CatalogoMarcas, title: 'Marcas · Panel' },
          { path: ':productoId', component: ProductoFormulario, title: 'Producto · Panel' }
        ]
      },
      {
        path: 'inventario',
        canActivate: [rolesGuard(ROLES_INVENTARIO)],
        children: [
          { path: '', component: InventarioStock, title: 'Inventario · Panel' },
          { path: ':sku', component: InventarioDetalle, title: 'SKU · Inventario' }
        ]
      },
      { path: 'pagos', component: AdminPagos, canActivate: [rolesGuard(ROLES_PAGOS)], title: 'Pagos · Panel' },
      {
        path: 'usuarios',
        canActivate: [rolesGuard(ROLES_USUARIOS)],
        children: [
          { path: '', component: AdminUsuarios, title: 'Usuarios · Panel' },
          { path: 'nuevo', component: UsuarioNuevo, title: 'Nuevo usuario · Panel' },
          { path: ':userId', component: UsuarioDetallePagina, title: 'Usuario · Panel' }
        ]
      }
    ]
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class AdminRoutingModule {}
