import { NgModule } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { SharedModule } from '../../shared/shared-module';
import { AuthRoutingModule } from './auth-routing-module';
import { Login } from './paginas/login/login';
import { Registro } from './paginas/registro/registro';
import { Perfil } from './paginas/perfil/perfil';

@NgModule({
  declarations: [Login, Registro, Perfil],
  imports: [SharedModule, ReactiveFormsModule, AuthRoutingModule]
})
export class AuthModule {}
