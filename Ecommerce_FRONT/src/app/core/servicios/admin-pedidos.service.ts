import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API } from '../config/api.config';
import { Pagina } from '../modelos/catalogo.model';
import { EstadoPedido, Pedido, PedidoResumen } from '../modelos/pedido.model';

export interface FiltroPedidosAdmin {
  pagina: number;
  tamano: number;
  estado?: EstadoPedido;
  numero?: string;
}

// Endpoints de Pedidos para el personal: ver todos los pedidos (Administrator, Support) y avanzarlos (Administrator).
@Injectable({ providedIn: 'root' })
export class AdminPedidosService {
  constructor(private http: HttpClient) {}

  listar(filtro: FiltroPedidosAdmin): Observable<Pagina<PedidoResumen>> {
    let parametros = new HttpParams().set('pagina', filtro.pagina).set('tamano', filtro.tamano);
    if (filtro.estado) parametros = parametros.set('estado', filtro.estado);
    if (filtro.numero) parametros = parametros.set('numero', filtro.numero.trim().toUpperCase());
    return this.http.get<Pagina<PedidoResumen>>(`${API.pedidos}/admin`, { params: parametros });
  }

  obtener(pedidoId: string): Observable<Pedido> {
    return this.http.get<Pedido>(`${API.pedidos}/admin/${pedidoId}`);
  }

  avanzar(pedidoId: string, estado: EstadoPedido, motivo?: string): Observable<Pedido> {
    return this.http.patch<Pedido>(`${API.pedidos}/${pedidoId}/estado`, { estado, motivo: motivo?.trim() || null });
  }
}
