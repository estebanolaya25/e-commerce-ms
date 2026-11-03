import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { interval } from 'rxjs';
import { ESTADOS_PEDIDO, EstadoPedido, Pedido } from '../../../../core/modelos/pedido.model';
import { AvisosService } from '../../../../core/servicios/avisos.service';
import { PedidosService } from '../../../../core/servicios/pedidos.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';
import { fechaUtc } from '../../../../core/utilidades/fechas';

// Etapas del despacho que se muestran como barra de progreso una vez pagado el pedido.
const ETAPAS: EstadoPedido[] = ['Pagado', 'EnPreparacion', 'Enviado', 'Entregado'];

@Component({
  selector: 'app-detalle-pedido',
  standalone: false,
  templateUrl: './detalle-pedido.html',
  styleUrl: './detalle-pedido.scss'
})
export class DetallePedido implements OnInit {
  private readonly destroyRef = inject(DestroyRef);

  pedido: Pedido | null = null;
  cargando = true;
  noEncontrado = false;
  confirmandoCancelacion = false;
  cancelando = false;
  restante = '';

  readonly etapas = ETAPAS.map(estado => ({ estado, ...ESTADOS_PEDIDO[estado] }));

  constructor(private ruta: ActivatedRoute, private pedidosService: PedidosService, private avisos: AvisosService) {}

  ngOnInit(): void {
    const pedidoId = this.ruta.snapshot.paramMap.get('pedidoId')!;
    this.pedidosService.obtener(pedidoId).subscribe({
      next: pedido => {
        this.pedido = pedido;
        this.cargando = false;
        this.actualizarRestante();
      },
      error: () => {
        this.noEncontrado = true;
        this.cargando = false;
      }
    });

    interval(1000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.actualizarRestante());
  }

  get puedePagar(): boolean {
    return this.pedido?.estado === 'PendientePago' && this.restante !== '' && this.restante !== '00:00';
  }

  get puedeCancelar(): boolean {
    return this.pedido?.estado === 'PendientePago';
  }

  // Posición del pedido en la barra de despacho; -1 si todavía no está pagado (o se canceló).
  get etapaActual(): number {
    return this.pedido ? ETAPAS.indexOf(this.pedido.estado) : -1;
  }

  fecha(texto: string): Date {
    return fechaUtc(texto);
  }

  opcionesTexto(opciones: string | null): string {
    if (!opciones) return '';
    try {
      return Object.values(JSON.parse(opciones) as Record<string, string>).join(' · ');
    } catch {
      return '';
    }
  }

  cancelar(): void {
    if (!this.pedido) return;
    this.cancelando = true;
    this.pedidosService.cancelar(this.pedido.pedidoId).subscribe({
      next: pedido => {
        this.pedido = pedido;
        this.cancelando = false;
        this.confirmandoCancelacion = false;
        this.avisos.info('Pedido cancelado', 'Las unidades reservadas volvieron al inventario.');
      },
      error: error => {
        this.cancelando = false;
        this.confirmandoCancelacion = false;
        this.avisos.error('No se pudo cancelar', mensajeDeError(error));
      }
    });
  }

  private actualizarRestante(): void {
    if (this.pedido?.estado !== 'PendientePago') {
      this.restante = '';
      return;
    }
    const milisegundos = fechaUtc(this.pedido.expiraEn).getTime() - Date.now();
    if (milisegundos <= 0) {
      this.restante = '00:00';
      return;
    }
    const minutos = Math.floor(milisegundos / 60000);
    const segundos = Math.floor((milisegundos % 60000) / 1000);
    this.restante = `${String(minutos).padStart(2, '0')}:${String(segundos).padStart(2, '0')}`;
  }
}
