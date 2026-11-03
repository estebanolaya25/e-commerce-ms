import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { Detalle } from './paginas/detalle/detalle';
import { Inicio } from './paginas/inicio/inicio';
import { Listado } from './paginas/listado/listado';

const routes: Routes = [
  { path: '', component: Inicio, title: 'Tienda Andina' },
  { path: 'productos', component: Listado, title: 'Productos · Tienda Andina' },
  { path: 'producto/:slug', component: Detalle }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class CatalogoRoutingModule {}
