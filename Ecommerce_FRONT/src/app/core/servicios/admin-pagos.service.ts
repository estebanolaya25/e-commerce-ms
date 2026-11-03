import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API } from '../config/api.config';
import { Pagina } from '../modelos/catalogo.model';
import { EstadoPago, Pago } from '../modelos/pago.model';

export interface FiltroPagos {
  pagina: number;
  tamano: number;
  estado?: EstadoPago;
  pedidoId?: string;
}

// Consulta de pagos para el personal (Administrator y Support). Pagos no expone escritura
// pública: aprobar, rechazar y reembolsar lo hace el propio sistema (simulador y saga de Pedidos).
@Injectable({ providedIn: 'root' })
export class AdminPagosService {
  constructor(private http: HttpClient) {}

  listar(filtro: FiltroPagos): Observable<Pagina<Pago>> {
    let parametros = new HttpParams().set('pagina', filtro.pagina).set('tamano', filtro.tamano);
    if (filtro.estado) parametros = parametros.set('estado', filtro.estado);
    if (filtro.pedidoId) parametros = parametros.set('pedidoId', filtro.pedidoId);
    return this.http.get<Pagina<Pago>>(API.pagos, { params: parametros });
  }
}
