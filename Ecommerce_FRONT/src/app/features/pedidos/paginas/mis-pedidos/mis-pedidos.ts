import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Pagina } from '../../../../core/modelos/catalogo.model';
import { ESTADOS_PEDIDO, EstadoPedido, PedidoResumen } from '../../../../core/modelos/pedido.model';
import { PedidosService } from '../../../../core/servicios/pedidos.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';
import { fechaUtc } from '../../../../core/utilidades/fechas';

const TAMANO_PAGINA = 10;

@Component({
  selector: 'app-mis-pedidos',
  standalone: false,
  templateUrl: './mis-pedidos.html',
  styleUrl: './mis-pedidos.scss'
})
export class MisPedidos implements OnInit {
  resultado: Pagina<PedidoResumen> | null = null;
  cargando = true;
  error: string | null = null;
  estado: EstadoPedido | '' = '';
  pagina = 1;

  readonly estados = Object.entries(ESTADOS_PEDIDO).map(([valor, info]) => ({ valor: valor as EstadoPedido, texto: info.texto }));

  constructor(private pedidosService: PedidosService, private ruta: ActivatedRoute, private router: Router) {}

  ngOnInit(): void {
    this.ruta.queryParamMap.subscribe(parametros => {
      this.estado = (parametros.get('estado') as EstadoPedido | null) ?? '';
      this.pagina = Math.max(1, Number(parametros.get('pagina')) || 1);
      this.cargar();
    });
  }

  filtrar(estado: EstadoPedido | ''): void {
    this.router.navigate([], { queryParams: { estado: estado || null, pagina: null }, queryParamsHandling: 'merge' });
  }

  irAPagina(pagina: number): void {
    this.router.navigate([], { queryParams: { pagina }, queryParamsHandling: 'merge' });
  }

  fecha(texto: string): Date {
    return fechaUtc(texto);
  }

  esPendiente(pedido: PedidoResumen): boolean {
    return pedido.estado === 'PendientePago' && fechaUtc(pedido.expiraEn).getTime() > Date.now();
  }

  private cargar(): void {
    this.cargando = true;
    this.error = null;
    this.pedidosService.listar(this.pagina, TAMANO_PAGINA, this.estado || undefined).subscribe({
      next: resultado => {
        this.resultado = resultado;
        this.cargando = false;
      },
      error: error => {
        this.error = mensajeDeError(error, 'No se pudieron cargar sus pedidos.');
        this.cargando = false;
      }
    });
  }
}
