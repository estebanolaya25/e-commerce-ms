import { Component, Input } from '@angular/core';

export type PestanaCatalogo = 'productos' | 'categorias' | 'marcas';

@Component({
  selector: 'app-pestanas-catalogo',
  standalone: false,
  templateUrl: './pestanas-catalogo.html',
  styleUrl: './pestanas-catalogo.scss'
})
export class PestanasCatalogo {
  @Input({ required: true }) activa!: PestanaCatalogo;

  readonly pestanas: { clave: PestanaCatalogo; texto: string; icono: string; ruta: string }[] = [
    { clave: 'productos', texto: 'Productos', icono: 'bi-bag', ruta: '/admin/catalogo' },
    { clave: 'categorias', texto: 'Categorías', icono: 'bi-diagram-3', ruta: '/admin/catalogo/categorias' },
    { clave: 'marcas', texto: 'Marcas', icono: 'bi-award', ruta: '/admin/catalogo/marcas' }
  ];
}
