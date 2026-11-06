import { Component, ElementRef, HostListener, OnDestroy, ViewChild } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Observable, Subscription, distinctUntilChanged, filter, map, skip } from 'rxjs';
import { AccionEnChat, MensajeAsistente } from '../../../core/modelos/asistente.model';
import { AsistenteService } from '../../../core/servicios/asistente.service';
import { CarritoService } from '../../../core/servicios/carrito.service';
import { PedidosService } from '../../../core/servicios/pedidos.service';
import { SesionService } from '../../../core/servicios/sesion.service';
import { mensajeDeError } from '../../../core/utilidades/errores-http';

const CLAVE_HISTORIAL = 'tienda.asistente.historial';
const MAX_CARACTERES = 1000;

// Chat flotante con el asistente virtual. Vive en la raíz de la app: la conversación sigue al cambiar de página
// y se guarda en sessionStorage para que sobreviva a recargar la pestaña (no a cerrarla).
// Con sesión, la conversación puede traer datos de los pedidos del cliente: se guarda por usuario y se borra al
// cerrar sesión, para que otra persona en el mismo navegador no la vea.
@Component({
  selector: 'app-asistente-chat',
  standalone: false,
  templateUrl: './asistente-chat.html',
  styleUrl: './asistente-chat.scss'
})
export class AsistenteChat implements OnDestroy {
  @ViewChild('entrada') private entrada?: ElementRef<HTMLTextAreaElement>;
  @ViewChild('lista') private lista?: ElementRef<HTMLDivElement>;

  readonly maxCaracteres = MAX_CARACTERES;
  readonly sugerencias = [
    'Busco una chaqueta para la lluvia',
    '¿Cuánto cuesta el envío?',
    '¿Cómo cancelo un pedido?',
    '¿Cómo sé qué talla soy?'
  ];

  abierto = false;
  visible = true;
  respondiendo = false;
  error: string | null = null;
  texto = '';
  private idUsuario: string | null;
  mensajes: MensajeAsistente[];

  private readonly navegacion: Subscription;
  private readonly cambioDeSesion: Subscription;

  constructor(
    private asistente: AsistenteService,
    private carrito: CarritoService,
    private pedidos: PedidosService,
    sesion: SesionService,
    router: Router
  ) {
    this.idUsuario = sesion.usuario?.id ?? null;
    this.mensajes = this.leerHistorial();

    this.cambioDeSesion = sesion.usuario$
      .pipe(map(usuario => usuario?.id ?? null), distinctUntilChanged(), skip(1))
      .subscribe(id => {
        if (this.idUsuario !== null) {
          this.borrarHistorial(this.idUsuario);
        }
        this.idUsuario = id;
        this.mensajes = this.leerHistorial();
        this.error = null;
      });

    // El panel interno tiene su propio flujo de trabajo; en esta fase el asistente es solo para clientes.
    const actualizarVisibilidad = (url: string) => (this.visible = !url.startsWith('/admin'));
    actualizarVisibilidad(router.url);
    this.navegacion = router.events
      .pipe(filter(evento => evento instanceof NavigationEnd))
      .subscribe(evento => actualizarVisibilidad((evento as NavigationEnd).urlAfterRedirects));
  }

  ngOnDestroy(): void {
    this.navegacion.unsubscribe();
    this.cambioDeSesion.unsubscribe();
  }

  // La acción la ejecuta el frontend con la sesión del cliente, por la misma API que usan el carrito y «Mis pedidos».
  confirmar(accion: AccionEnChat): void {
    if (accion.estado !== 'pendiente') {
      return;
    }
    let operacion: Observable<unknown>;
    let exito: string;
    if (accion.tipo === 'agregar_carrito' && accion.sku && accion.cantidad) {
      operacion = this.carrito.agregar(accion.sku, accion.cantidad);
      exito = 'Agregado al carrito.';
    } else if (accion.tipo === 'cancelar_pedido' && accion.pedidoId) {
      operacion = this.pedidos.cancelar(accion.pedidoId);
      exito = accion.numero ? `Pedido ${accion.numero} cancelado.` : 'Pedido cancelado.';
    } else {
      accion.estado = 'error';
      accion.mensaje = 'No se pudo realizar esta acción.';
      return;
    }

    accion.estado = 'ejecutando';
    operacion.subscribe({
      next: () => {
        accion.estado = 'hecha';
        accion.mensaje = exito;
        this.guardarHistorial();
      },
      error: error => {
        accion.estado = 'error';
        accion.mensaje = mensajeDeError(error, 'No se pudo realizar la acción. Intente de nuevo.');
        this.guardarHistorial();
      }
    });
  }

  descartar(accion: AccionEnChat): void {
    if (accion.estado === 'pendiente') {
      accion.estado = 'descartada';
      this.guardarHistorial();
    }
  }

  alternar(): void {
    this.abierto = !this.abierto;
    if (this.abierto) {
      this.enfocarEntrada();
      this.bajarAlFinal();
    }
  }

  @HostListener('document:keydown.escape')
  cerrar(): void {
    this.abierto = false;
  }

  alEscribir(evento: KeyboardEvent): void {
    // Enter envía; Shift+Enter hace un salto de línea.
    if (evento.key === 'Enter' && !evento.shiftKey) {
      evento.preventDefault();
      this.enviar();
    }
  }

  enviar(pregunta = this.texto): void {
    const contenido = pregunta.trim().slice(0, MAX_CARACTERES);
    if (!contenido || this.respondiendo) {
      return;
    }
    this.texto = '';
    this.mensajes = [...this.mensajes, { role: 'user', content: contenido }];
    this.solicitarRespuesta();
  }

  // Tras un error, la última pregunta sigue en la conversación: se vuelve a pedir la respuesta.
  reintentar(): void {
    if (!this.respondiendo && this.mensajes.at(-1)?.role === 'user') {
      this.solicitarRespuesta();
    }
  }

  // En celular el chat ocupa toda la pantalla: al abrir un producto se cierra para que se vea la página.
  alAbrirProducto(): void {
    if (window.matchMedia('(max-width: 575.98px)').matches) {
      this.cerrar();
    }
  }

  nuevaConversacion(): void {
    if (this.respondiendo) {
      return;
    }
    this.mensajes = [];
    this.error = null;
    this.guardarHistorial();
    this.enfocarEntrada();
  }

  // Convierte el markdown sencillo que usa el modelo (negritas, viñetas, saltos de línea) en HTML.
  // Se escapa todo primero: el texto del modelo nunca se inserta como HTML sin escapar.
  formatear(texto: string): string {
    const escapado = texto
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');

    const html: string[] = [];
    let enLista = false;
    for (const linea of escapado.split('\n')) {
      const vineta = /^\s*(?:[-*•]|\d+[.)])\s+(.*)$/.exec(linea);
      if (vineta) {
        if (!enLista) {
          html.push('<ul>');
          enLista = true;
        }
        html.push(`<li>${this.negritas(vineta[1])}</li>`);
        continue;
      }
      if (enLista) {
        html.push('</ul>');
        enLista = false;
      }
      html.push(linea.trim() ? `<p>${this.negritas(linea)}</p>` : '');
    }
    if (enLista) {
      html.push('</ul>');
    }
    return html.join('');
  }

  private negritas(linea: string): string {
    return linea.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
  }

  private solicitarRespuesta(): void {
    this.error = null;
    this.respondiendo = true;
    this.guardarHistorial();
    this.bajarAlFinal();

    this.asistente.preguntar(this.mensajes).subscribe({
      next: respuesta => {
        this.mensajes = [...this.mensajes, {
          role: 'assistant',
          content: respuesta.reply,
          // Si recomendó productos, las fuentes de la base de conocimiento no aportan: se muestran las tarjetas.
          fuentes: respuesta.productos?.length || respuesta.acciones?.length ? [] : respuesta.sources,
          productos: respuesta.productos ?? [],
          acciones: (respuesta.acciones ?? []).map(accion => ({ ...accion, estado: 'pendiente' as const }))
        }];
        this.respondiendo = false;
        this.guardarHistorial();
        this.bajarAlFinal();
        this.enfocarEntrada();
      },
      error: error => {
        this.error = mensajeDeError(error, 'No pude responder en este momento. Intente de nuevo.');
        this.respondiendo = false;
        this.bajarAlFinal();
      }
    });
  }

  private enfocarEntrada(): void {
    setTimeout(() => this.entrada?.nativeElement.focus());
  }

  private bajarAlFinal(): void {
    setTimeout(() => {
      const lista = this.lista?.nativeElement;
      if (lista) {
        lista.scrollTop = lista.scrollHeight;
      }
    });
  }

  private clave(idUsuario: string | null): string {
    return `${CLAVE_HISTORIAL}.${idUsuario ?? 'invitado'}`;
  }

  private leerHistorial(): MensajeAsistente[] {
    try {
      const guardado = JSON.parse(sessionStorage.getItem(this.clave(this.idUsuario)) ?? '[]');
      if (!Array.isArray(guardado)) {
        return [];
      }
      // Una acción que quedó a medias al recargar la página se vuelve a ofrecer (el cliente decide de nuevo).
      for (const mensaje of guardado as MensajeAsistente[]) {
        for (const accion of mensaje.acciones ?? []) {
          if (accion.estado === 'ejecutando') {
            accion.estado = 'pendiente';
          }
        }
      }
      return guardado;
    } catch {
      return [];
    }
  }

  private borrarHistorial(idUsuario: string): void {
    try {
      sessionStorage.removeItem(this.clave(idUsuario));
    } catch {
      // Sin almacenamiento: no hay nada que borrar.
    }
  }

  private guardarHistorial(): void {
    try {
      sessionStorage.setItem(this.clave(this.idUsuario), JSON.stringify(this.mensajes));
    } catch {
      // Sin almacenamiento (modo privado o bloqueado): la conversación solo dura mientras la página esté abierta.
    }
  }
}
