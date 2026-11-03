import { Component } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';

@Component({
  selector: 'app-root',
  standalone: false,
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  anuncio = '';

  constructor(router: Router, titulo: Title) {
    router.events.pipe(filter(evento => evento instanceof NavigationEnd)).subscribe(() => {
      // El título lo asigna el router después del evento; se lee en el siguiente ciclo.
      setTimeout(() => (this.anuncio = titulo.getTitle()));
    });
  }

  irAlContenido(evento: Event): void {
    evento.preventDefault();
    document.getElementById('contenido')?.focus();
  }
}
