import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Pagina } from '../../../../core/modelos/catalogo.model';
import { esSoloBodega } from '../../../../core/config/roles';
import { ESTADOS_DESPACHABLES, ESTADOS_PEDIDO, EstadoPedido, PedidoResumen } from '../../../../core/modelos/pedido.model';
import { AdminPedidosService } from '../../../../core/servicios/admin-pedidos.service';
import { SesionService } from '../../../../core/servicios/sesion.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';
import { fechaUtc } from '../../../../core/utilidades/fechas';

const TAMANO_PAGINA = 15;

@Component({
  selector: 'app-admin-pedidos',
  standalone: false,
  templateUrl: './pedidos.html',
  styleUrl: './pedidos.scss'
})
export class AdminPedidos implements OnInit {
  resultado: Pagina<PedidoResumen> | null = null;
  cargando = true;
  error: string | null = null;
  estado: EstadoPedido | '' = '';
  numero = '';
  pagina = 1;

  readonly soloBodega: boolean;
  readonly estados: { valor: EstadoPedido; texto: string }[];

  constructor(private adminPedidos: AdminPedidosService, private ruta: ActivatedRoute, private router: Router, sesion: SesionService) {
    this.soloBodega = esSoloBodega(sesion);
    const visibles = this.soloBodega ? ESTADOS_DESPACHABLES : (Object.keys(ESTADOS_PEDIDO) as EstadoPedido[]);
    this.estados = visibles.map(valor => ({ valor, texto: ESTADOS_PEDIDO[valor].texto }));
  }

  ngOnInit(): void {
    this.ruta.queryParamMap.subscribe(parametros => {
      this.estado = (parametros.get('estado') as EstadoPedido | null) ?? '';
      this.numero = parametros.get('numero') ?? '';
      this.pagina = Math.max(1, Number(parametros.get('pagina')) || 1);
      this.cargar();
    });
  }

  filtrarEstado(estado: EstadoPedido | ''): void {
    this.navegar({ estado: estado || null, pagina: null });
  }

  buscarNumero(numero: string): void {
    this.navegar({ numero: numero.trim() || null, pagina: null });
  }

  limpiar(): void {
    this.router.navigate([], { relativeTo: this.ruta, queryParams: {} });
  }

  irAPagina(pagina: number): void {
    this.navegar({ pagina });
  }

  fecha(texto: string): Date {
    return fechaUtc(texto);
  }

  private navegar(cambios: Record<string, string | number | null>): void {
    this.router.navigate([], { relativeTo: this.ruta, queryParams: cambios, queryParamsHandling: 'merge' });
  }

  private cargar(): void {
    this.cargando = true;
    this.error = null;
    this.adminPedidos
      .listar({ pagina: this.pagina, tamano: TAMANO_PAGINA, estado: this.estado || undefined, numero: this.numero || undefined })
      .subscribe({
        next: resultado => {
          this.resultado = resultado;
          this.cargando = false;
        },
        error: error => {
          this.error = mensajeDeError(error, 'No se pudieron cargar los pedidos.');
          this.cargando = false;
        }
      });
  }
}
