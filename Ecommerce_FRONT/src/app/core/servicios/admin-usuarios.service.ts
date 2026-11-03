import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API } from '../config/api.config';
import { Pagina } from '../modelos/catalogo.model';
import { CrearUsuarioPeticion, RolInfo, UsuarioDetalle, UsuarioResumen } from '../modelos/usuario-admin.model';

export interface FiltroUsuarios {
  pagina: number;
  tamano: number;
  buscar?: string;
  rol?: string;
  activo?: boolean;
}

// Gestión de usuarios del microservicio Auth (solo Administrator).
@Injectable({ providedIn: 'root' })
export class AdminUsuariosService {
  private readonly base = `${API.auth}/usuarios`;

  constructor(private http: HttpClient) {}

  listar(filtro: FiltroUsuarios): Observable<Pagina<UsuarioResumen>> {
    let parametros = new HttpParams().set('pagina', filtro.pagina).set('tamano', filtro.tamano);
    if (filtro.buscar) parametros = parametros.set('buscar', filtro.buscar.trim());
    if (filtro.rol) parametros = parametros.set('rol', filtro.rol);
    if (filtro.activo !== undefined) parametros = parametros.set('activo', filtro.activo);
    return this.http.get<Pagina<UsuarioResumen>>(this.base, { params: parametros });
  }

  obtener(userId: string): Observable<UsuarioDetalle> {
    return this.http.get<UsuarioDetalle>(`${this.base}/${userId}`);
  }

  roles(): Observable<RolInfo[]> {
    return this.http.get<RolInfo[]>(`${API.auth}/roles`);
  }

  crear(peticion: CrearUsuarioPeticion): Observable<UsuarioDetalle> {
    return this.http.post<UsuarioDetalle>(this.base, peticion);
  }

  cambiarRoles(userId: string, roles: string[]): Observable<UsuarioDetalle> {
    return this.http.put<UsuarioDetalle>(`${this.base}/${userId}/roles`, { roles });
  }

  cambiarEstado(userId: string, activo: boolean): Observable<UsuarioDetalle> {
    return this.http.patch<UsuarioDetalle>(`${this.base}/${userId}/estado`, { activo });
  }

  desbloquear(userId: string): Observable<UsuarioDetalle> {
    return this.http.post<UsuarioDetalle>(`${this.base}/${userId}/desbloquear`, {});
  }
}
