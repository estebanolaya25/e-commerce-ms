import { Component } from '@angular/core';

@Component({
  selector: 'app-pie-pagina',
  standalone: false,
  templateUrl: './pie-pagina.html',
  styleUrl: './pie-pagina.scss'
})
export class PiePagina {
  readonly anio = new Date().getFullYear();
}
