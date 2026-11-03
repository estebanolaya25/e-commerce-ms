import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';
import { NOMBRES_ROL, ROLES } from '../../../../core/config/roles';
import { EVENTOS_AUDITORIA, EventoAuditoria, RolInfo, UsuarioDetalle } from '../../../../core/modelos/usuario-admin.model';
import { AdminUsuariosService } from '../../../../core/servicios/admin-usuarios.service';
import { AvisosService } from '../../../../core/servicios/avisos.service';
import { SesionService } from '../../../../core/servicios/sesion.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';
import { fechaUtc } from '../../../../core/utilidades/fechas';

@Component({
  selector: 'app-usuario-detalle',
  standalone: false,
  templateUrl: './usuario-detalle.html',
  styleUrl: './usuario-detalle.scss'
})
export class UsuarioDetallePagina implements OnInit {
  usuario: UsuarioDetalle | null = null;
  roles: RolInfo[] = [];
  seleccion = new Set<string>();
  cargando = true;
  noEncontrado = false;
  guardandoRoles = false;
  ocupado = false;
  readonly nombresRol = NOMBRES_ROL;
  private readonly miId: string | null;

  constructor(
    private ruta: ActivatedRoute,
    private adminUsuarios: AdminUsuariosService,
    private avisos: AvisosService,
    sesion: SesionService
  ) {
    this.miId = sesion.usuario?.id ?? null;
  }

  ngOnInit(): void {
    forkJoin({
      usuario: this.adminUsuarios.obtener(this.ruta.snapshot.paramMap.get('userId')!),
      roles: this.adminUsuarios.roles()
    }).subscribe({
      next: ({ usuario, roles }) => {
        this.roles = roles;
        this.aplicar(usuario);
        this.cargando = false;
      },
      error: () => {
        this.noEncontrado = true;
        this.cargando = false;
      }
    });
  }

  // El backend tampoco lo permite; aquí se explica antes de intentarlo.
  get esYo(): boolean {
    return this.usuario?.userId === this.miId;
  }

  get rolesCambiaron(): boolean {
    const actuales = this.usuario?.roles ?? [];
    return actuales.length !== this.seleccion.size || actuales.some(r => !this.seleccion.has(r));
  }

  bloqueadoRol(rol: string): boolean {
    return this.esYo && rol === ROLES.administrador;
  }

  alternarRol(rol: string): void {
    if (this.bloqueadoRol(rol)) return;
    if (this.seleccion.has(rol)) {
      this.seleccion.delete(rol);
    } else {
      this.seleccion.add(rol);
    }
  }

  deshacerRoles(): void {
    this.seleccion = new Set(this.usuario?.roles ?? []);
  }

  guardarRoles(): void {
    if (!this.usuario || this.seleccion.size === 0) return;
    this.guardandoRoles = true;
    this.adminUsuarios.cambiarRoles(this.usuario.userId, [...this.seleccion]).subscribe({
      next: usuario => {
        this.aplicar(usuario);
        this.guardandoRoles = false;
        this.avisos.exito('Roles actualizados', 'Se aplican cuando el usuario renueve su sesión, en pocos minutos.');
      },
      error: error => {
        this.guardandoRoles = false;
        this.avisos.error('No se pudieron cambiar los roles', mensajeDeError(error));
      }
    });
  }

  cambiarEstado(): void {
    if (!this.usuario) return;
    const activo = !this.usuario.activo;
    if (!activo && !confirm(`¿Desactivar a ${this.usuario.nombre} ${this.usuario.apellido}? No podrá iniciar sesión y se cerrarán sus sesiones abiertas.`)) return;

    this.ejecutar(this.adminUsuarios.cambiarEstado(this.usuario.userId, activo), activo ? 'Cuenta activada' : 'Cuenta desactivada');
  }

  desbloquear(): void {
    if (!this.usuario) return;
    this.ejecutar(this.adminUsuarios.desbloquear(this.usuario.userId), 'Cuenta desbloqueada');
  }

  evento(e: EventoAuditoria) {
    return EVENTOS_AUDITORIA[e.tipo] ?? { texto: e.tipo, icono: 'bi-dot', clase: 'neutro' };
  }

  fecha(texto: string): Date {
    return fechaUtc(texto);
  }

  private ejecutar(llamada: ReturnType<AdminUsuariosService['obtener']>, titulo: string): void {
    this.ocupado = true;
    llamada.subscribe({
      next: usuario => {
        this.aplicar(usuario);
        this.ocupado = false;
        this.avisos.exito(titulo, `${usuario.nombre} ${usuario.apellido}`);
      },
      error: error => {
        this.ocupado = false;
        this.avisos.error('No se pudo completar', mensajeDeError(error));
      }
    });
  }

  private aplicar(usuario: UsuarioDetalle): void {
    this.usuario = usuario;
    this.seleccion = new Set(usuario.roles);
  }
}
