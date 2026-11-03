import { Component } from '@angular/core';
import { NOMBRES_ROL, ROLES_CATALOGO, ROLES_INVENTARIO, ROLES_PAGOS, ROLES_PANEL, ROLES_PEDIDOS, ROLES_USUARIOS } from '../../../../core/config/roles';
import { SesionService } from '../../../../core/servicios/sesion.service';

interface SeccionPanel {
  texto: string;
  icono: string;
  ruta: string;
  roles: readonly string[];
}

@Component({
  selector: 'app-panel-admin',
  standalone: false,
  templateUrl: './panel-admin.html',
  styleUrl: './panel-admin.scss'
})
export class PanelAdmin {
  private readonly secciones: SeccionPanel[] = [
    { texto: 'Resumen', icono: 'bi-speedometer2', ruta: '/admin', roles: ROLES_PANEL },
    { texto: 'Pedidos', icono: 'bi-receipt', ruta: '/admin/pedidos', roles: ROLES_PEDIDOS },
    { texto: 'Catálogo', icono: 'bi-grid', ruta: '/admin/catalogo', roles: ROLES_CATALOGO },
    { texto: 'Inventario', icono: 'bi-boxes', ruta: '/admin/inventario', roles: ROLES_INVENTARIO },
    { texto: 'Pagos', icono: 'bi-credit-card', ruta: '/admin/pagos', roles: ROLES_PAGOS },
    { texto: 'Usuarios', icono: 'bi-people', ruta: '/admin/usuarios', roles: ROLES_USUARIOS }
  ];

  // Solo los roles de trabajo: "Cliente" no aporta en la cabecera del panel.
  readonly nombresRol: Record<string, string> = Object.fromEntries(
    Object.entries(NOMBRES_ROL).filter(([rol]) => ROLES_PANEL.includes(rol as (typeof ROLES_PANEL)[number]))
  ) as Record<string, string>;

  constructor(private sesion: SesionService) {}

  // Cada usuario ve solo las secciones de sus roles.
  get menu(): SeccionPanel[] {
    return this.secciones.filter(s => this.sesion.tieneAlgunRol(s.roles));
  }

  get rolesUsuario(): string[] {
    return (this.sesion.usuario?.roles ?? []).filter(r => r in this.nombresRol).map(r => this.nombresRol[r]);
  }

  get nombre(): string {
    const usuario = this.sesion.usuario;
    return usuario ? `${usuario.nombre} ${usuario.apellido}`.trim() : '';
  }
}
