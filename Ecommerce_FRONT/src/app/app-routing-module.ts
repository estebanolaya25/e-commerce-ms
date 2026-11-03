import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ROLES_PANEL } from './core/config/roles';
import { autenticadoGuard, rolesGuard } from './core/guards/sesion.guards';
import { NoEncontrada } from './shared/paginas/no-encontrada/no-encontrada';

// Cada módulo de funcionalidad se descarga solo cuando el usuario entra a su ruta (lazy loading).
const routes: Routes = [
  { path: '', loadChildren: () => import('./features/catalogo/catalogo-module').then(m => m.CatalogoModule) },
  { path: 'cuenta', loadChildren: () => import('./features/auth/auth-module').then(m => m.AuthModule) },
  {
    path: 'carrito',
    canActivate: [autenticadoGuard],
    loadChildren: () => import('./features/carrito/carrito-module').then(m => m.CarritoModule)
  },
  {
    path: 'checkout',
    canActivate: [autenticadoGuard],
    loadChildren: () => import('./features/checkout/checkout-module').then(m => m.CheckoutModule)
  },
  {
    path: 'pedidos',
    canActivate: [autenticadoGuard],
    loadChildren: () => import('./features/pedidos/pedidos-module').then(m => m.PedidosModule)
  },
  {
    path: 'admin',
    canActivate: [rolesGuard(ROLES_PANEL)],
    loadChildren: () => import('./features/admin/admin-module').then(m => m.AdminModule)
  },
  { path: '**', component: NoEncontrada }
];

@NgModule({
  imports: [RouterModule.forRoot(routes, { scrollPositionRestoration: 'top' })],
  exports: [RouterModule]
})
export class AppRoutingModule {}
