import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SharedModule } from '../../shared/shared-module';
import { CatalogoRoutingModule } from './catalogo-routing-module';
import { Detalle } from './paginas/detalle/detalle';
import { Inicio } from './paginas/inicio/inicio';
import { Listado } from './paginas/listado/listado';

@NgModule({
  declarations: [Inicio, Listado, Detalle],
  imports: [SharedModule, FormsModule, CatalogoRoutingModule]
})
export class CatalogoModule {}
