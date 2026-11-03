import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { NOMBRES_ROL } from '../../../../core/config/roles';
import { Pagina } from '../../../../core/modelos/catalogo.model';
import { RolInfo, UsuarioResumen } from '../../../../core/modelos/usuario-admin.model';
import { AdminUsuariosService } from '../../../../core/servicios/admin-usuarios.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';
import { fechaUtc } from '../../../../core/utilidades/fechas';

const TAMANO_PAGINA = 20;

@Component({
  selector: 'app-usuarios',
  standalone: false,
  templateUrl: './usuarios.html',
  styleUrl: './usuarios.scss'
})
export class AdminUsuarios implements OnInit {
  resultado: Pagina<UsuarioResumen> | null = null;
  roles: RolInfo[] = [];
  cargando = true;
  error: string | null = null;
  buscar = '';
  rol = '';
  estado: '' | 'activos' | 'inactivos' = '';
  pagina = 1;
  readonly nombresRol = NOMBRES_ROL;

  constructor(private adminUsuarios: AdminUsuariosService, private ruta: ActivatedRoute, private router: Router) {}

  ngOnInit(): void {
    this.adminUsuarios.roles().subscribe(roles => (this.roles = roles));
    this.ruta.queryParamMap.subscribe(parametros => {
      this.buscar = parametros.get('buscar') ?? '';
      this.rol = parametros.get('rol') ?? '';
      const estado = parametros.get('estado');
      this.estado = estado === 'activos' || estado === 'inactivos' ? estado : '';
      this.pagina = Math.max(1, Number(parametros.get('pagina')) || 1);
      this.cargar();
    });
  }

  get hayFiltros(): boolean {
    return !!(this.buscar || this.rol || this.estado);
  }

  filtrar(cambios: Record<string, string>): void {
    const queryParams: Record<string, string | null> = { pagina: null };
    for (const [clave, valor] of Object.entries(cambios)) queryParams[clave] = valor.trim() || null;
    this.router.navigate([], { relativeTo: this.ruta, queryParams, queryParamsHandling: 'merge' });
  }

  limpiar(): void {
    this.router.navigate([], { relativeTo: this.ruta, queryParams: {} });
  }

  irAPagina(pagina: number): void {
    this.router.navigate([], { relativeTo: this.ruta, queryParams: { pagina }, queryParamsHandling: 'merge' });
  }

  iniciales(u: UsuarioResumen): string {
    return `${u.nombre.charAt(0)}${u.apellido.charAt(0)}`.toUpperCase();
  }

  fecha(texto: string): Date {
    return fechaUtc(texto);
  }

  private cargar(): void {
    this.cargando = true;
    this.error = null;
    this.adminUsuarios
      .listar({
        pagina: this.pagina,
        tamano: TAMANO_PAGINA,
        buscar: this.buscar || undefined,
        rol: this.rol || undefined,
        activo: this.estado === '' ? undefined : this.estado === 'activos'
      })
      .subscribe({
        next: resultado => {
          this.resultado = resultado;
          this.cargando = false;
        },
        error: error => {
          this.error = mensajeDeError(error, 'No se pudieron cargar los usuarios.');
          this.cargando = false;
        }
      });
  }
}
