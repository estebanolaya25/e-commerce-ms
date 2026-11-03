import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API } from '../config/api.config';
import { peticionSilenciosa } from '../interceptores/contexto-http';
import { Pagina } from '../modelos/catalogo.model';
import { DireccionEnvio, EstadoPedido, PagoIniciado, Pedido, PedidoResumen, Tarjeta } from '../modelos/pedido.model';

@Injectable({ providedIn: 'root' })
export class PedidosService {
  constructor(private http: HttpClient) {}

  // La misma Idempotency-Key en dos envíos devuelve el mismo pedido: un doble clic no compra dos veces.
  crear(direccion: DireccionEnvio, claveIdempotencia: string): Observable<Pedido> {
    const cabeceras = new HttpHeaders({ 'Idempotency-Key': claveIdempotencia });
    return this.http.post<Pedido>(API.pedidos, { direccion }, { headers: cabeceras });
  }

  listar(pagina: number, tamano: number, estado?: EstadoPedido): Observable<Pagina<PedidoResumen>> {
    let parametros = new HttpParams().set('pagina', pagina).set('tamano', tamano);
    if (estado) parametros = parametros.set('estado', estado);
    return this.http.get<Pagina<PedidoResumen>>(API.pedidos, { params: parametros });
  }

  obtener(pedidoId: string, silenciosa = false): Observable<Pedido> {
    return this.http.get<Pedido>(`${API.pedidos}/${pedidoId}`, silenciosa ? { context: peticionSilenciosa() } : {});
  }

  // Responde 202: el resultado del pago llega después (Pagos lo decide en 2 a 8 segundos).
  pagar(pedidoId: string, tarjeta: Tarjeta): Observable<PagoIniciado> {
    return this.http.post<PagoIniciado>(`${API.pedidos}/${pedidoId}/pagar`, { tarjeta });
  }

  // Solo pedidos pendientes de pago: Pedidos libera la reserva en Inventario y lo marca Cancelado.
  cancelar(pedidoId: string): Observable<Pedido> {
    return this.http.post<Pedido>(`${API.pedidos}/${pedidoId}/cancelar`, {});
  }
}
