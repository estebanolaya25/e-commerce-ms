import { Component } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { ROLES_PANEL } from '../../../core/config/roles';
import { AuthService } from '../../../core/servicios/auth.service';
import { CarritoService } from '../../../core/servicios/carrito.service';
import { SesionService } from '../../../core/servicios/sesion.service';

@Component({
  selector: 'app-navbar',
  standalone: false,
  templateUrl: './navbar.html',
  styleUrl: './navbar.scss'
})
export class Navbar {
  readonly usuario$;
  readonly unidadesCarrito$;
  textoBusqueda = '';
  readonly rolesPanel = ROLES_PANEL;

  constructor(public sesion: SesionService, carrito: CarritoService, private authService: AuthService, private router: Router) {
    this.usuario$ = sesion.usuario$;
    this.unidadesCarrito$ = carrito.unidades$;
    this.router.events.pipe(filter(evento => evento instanceof NavigationEnd)).subscribe(() => {
      this.textoBusqueda = this.router.parseUrl(this.router.url).queryParams['buscar'] ?? '';
      this.cerrarMenuMovil();
    });
  }

  buscar(texto: string): void {
    const termino = texto.trim();
    this.router.navigate(['/productos'], { queryParams: termino ? { buscar: termino, orden: 'relevancia' } : {} });
  }

  // Bootstrap deja abierto el menú colapsable en móvil después de tocar un enlace; se cierra con su propia API.
  private cerrarMenuMovil(): void {
    const menu = document.getElementById('menuPrincipal');
    const bootstrap = (window as unknown as { bootstrap?: { Collapse: { getOrCreateInstance(e: Element, o: object): { hide(): void } } } }).bootstrap;
    if (menu?.classList.contains('show') && bootstrap) {
      bootstrap.Collapse.getOrCreateInstance(menu, { toggle: false }).hide();
    }
  }

  iniciales(nombre: string, apellido: string): string {
    return `${nombre.charAt(0)}${apellido.charAt(0)}`.toUpperCase();
  }

  cerrarSesion(): void {
    this.authService.cerrarSesion().subscribe(() => this.router.navigate(['/']));
  }
}
