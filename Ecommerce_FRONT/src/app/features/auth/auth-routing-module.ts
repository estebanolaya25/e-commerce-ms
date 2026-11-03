import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { autenticadoGuard, invitadoGuard } from '../../core/guards/sesion.guards';
import { Login } from './paginas/login/login';
import { Perfil } from './paginas/perfil/perfil';
import { Registro } from './paginas/registro/registro';

const routes: Routes = [
  { path: 'ingresar', component: Login, canActivate: [invitadoGuard], title: 'Ingresar · Tienda Andina' },
  { path: 'registro', component: Registro, canActivate: [invitadoGuard], title: 'Crear cuenta · Tienda Andina' },
  { path: 'perfil', component: Perfil, canActivate: [autenticadoGuard], title: 'Mi cuenta · Tienda Andina' },
  { path: '', pathMatch: 'full', redirectTo: 'ingresar' }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class AuthRoutingModule {}
