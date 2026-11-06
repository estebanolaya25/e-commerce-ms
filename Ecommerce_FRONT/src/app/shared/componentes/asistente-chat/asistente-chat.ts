import { Component, ElementRef, HostListener, OnDestroy, ViewChild } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Observable, Subscription, distinctUntilChanged, filter, map, skip } from 'rxjs';
import { ROLES } from '../../../core/config/roles';
import { AccionEnChat, MensajeAsistente, RespuestaAsistente } from '../../../core/modelos/asistente.model';
import { EstadoPedido } from '../../../core/modelos/pedido.model';
import { AdminPedidosService } from '../../../core/servicios/admin-pedidos.service';
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
// En /admin cambia a modo panel: otro endpoint (herramientas según el rol del empleado) y otra conversación.
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

  abierto = false;
  modo: 'tienda' | 'panel' = 'tienda';
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
    private adminPedidos: AdminPedidosService,
    private sesion: SesionService,
    router: Router
  ) {
    this.idUsuario = sesion.usuario?.id ?? null;
    this.modo = router.url.startsWith('/admin') ? 'panel' : 'tienda';
    this.mensajes = this.leerHistorial();

    this.cambioDeSesion = sesion.usuario$
      .pipe(map(usuario => usuario?.id ?? null), distinctUntilChanged(), skip(1))
      .subscribe(id => {
        if (this.idUsuario !== null) {
          this.borrarHistorial(this.idUsuario, 'tienda');
          this.borrarHistorial(this.idUsuario, 'panel');
        }
        this.idUsuario = id;
        this.mensajes = this.leerHistorial();
        this.error = null;
      });

    // Al entrar o salir de /admin cambia el asistente y su conversación (cada modo guarda la suya).
    this.navegacion = router.events
      .pipe(filter(evento => evento instanceof NavigationEnd))
      .subscribe(evento => {
        const modo = (evento as NavigationEnd).urlAfterRedirects.startsWith('/admin') ? 'panel' : 'tienda';
        if (modo !== this.modo && !this.respondiendo) {
          this.modo = modo;
          this.mensajes = this.leerHistorial();
          this.error = null;
        }
      });
  }

  get titulo(): string {
    return this.modo === 'panel' ? 'Andi · interno' : 'Andi';
  }

  get subtitulo(): string {
    return this.modo === 'panel' ? 'Asistente del equipo de Tienda Andina' : 'Asistente virtual de Tienda Andina';
  }

  get saludo(): string {
    if (this.modo === 'tienda') {
      return '¡Hola! Soy Andi. Puedo ayudarle a encontrar productos y resolver dudas sobre envíos, pagos, pedidos, devoluciones, tallas y su cuenta.';
    }
    const partes: string[] = [];
    if (this.sesion.tieneAlgunRol([ROLES.administrador, ROLES.soporte, ROLES.bodega])) partes.push('consultar pedidos');
    if (this.sesion.tieneAlgunRol([ROLES.administrador, ROLES.soporte])) partes.push('revisar pagos');
    if (this.sesion.tieneAlgunRol([ROLES.administrador, ROLES.bodega])) partes.push('ver el stock y preparar despachos');
    if (this.sesion.tieneAlgunRol([ROLES.administrador, ROLES.catalogo])) partes.push('buscar productos del catálogo');
    return `Hola, soy Andi. Según tu rol puedo ${partes.join(', ') || 'responder dudas sobre las políticas de la tienda'}.`;
  }

  get sugerencias(): string[] {
    if (this.modo === 'tienda') {
      return ['Busco una chaqueta para la lluvia', '¿Cuánto cuesta el envío?', '¿Cómo cancelo un pedido?', '¿Cómo sé qué talla soy?'];
    }
    const lista: string[] = [];
    if (this.sesion.tieneAlgunRol([ROLES.administrador, ROLES.bodega])) {
      lista.push('¿Qué pedidos pagados faltan por despachar?', '¿Qué SKU están bajo el stock mínimo?');
    }
    if (this.sesion.tieneAlgunRol([ROLES.administrador, ROLES.soporte])) {
      lista.push('¿Qué pedidos tienen el pago en proceso?', '¿Cuál es la política de devoluciones?');
    }
    if (this.sesion.tieneAlgunRol([ROLES.administrador, ROLES.catalogo])) {
      lista.push('¿Qué chaquetas tenemos en el catálogo?');
    }
    return [...new Set(lista)].slice(0, 4);
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
    } else if (accion.tipo === 'avanzar_pedido' && accion.pedidoId && accion.estadoNuevo) {
      operacion = this.adminPedidos.avanzar(accion.pedidoId, accion.estadoNuevo as EstadoPedido, 'Desde el asistente del panel');
      exito = accion.numero ? `Pedido ${accion.numero} actualizado.` : 'Pedido actualizado.';
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

    // La respuesta se va escribiendo en esta burbuja a medida que llega; al final se completa con productos y acciones.
    const historial = this.mensajes;
    const enCurso: MensajeAsistente = { role: 'assistant', content: '', enCurso: true };
    this.mensajes = [...historial, enCurso];
    let terminada = false;

    const fallar = (mensaje: string) => {
      terminada = true;
      this.mensajes = this.mensajes.filter(m => m !== enCurso);
      this.error = mensaje;
      this.respondiendo = false;
      this.bajarAlFinal();
    };

    this.asistente.preguntarEnVivo(historial, this.modo).subscribe({
      next: evento => {
        switch (evento.tipo) {
          case 'estado':
            enCurso.progreso = evento.texto;
            break;
          case 'texto':
            enCurso.content += evento.texto;
            enCurso.progreso = undefined;
            this.bajarAlFinal();
            break;
          case 'fin':
            terminada = true;
            this.completar(enCurso, evento.respuesta);
            break;
          case 'error':
            fallar(evento.mensaje);
            break;
        }
      },
      error: error => fallar(mensajeDeError(error, 'No pude responder en este momento. Intente de nuevo.')),
      complete: () => {
        if (!terminada) {
          fallar('La respuesta se interrumpió. Intente de nuevo.');
        }
      }
    });
  }

  private completar(mensaje: MensajeAsistente, respuesta: RespuestaAsistente): void {
    mensaje.content = respuesta.reply;
    // Si recomendó productos o propuso acciones, las fuentes de la base de conocimiento no aportan.
    mensaje.fuentes = respuesta.productos?.length || respuesta.acciones?.length ? [] : respuesta.sources;
    mensaje.productos = respuesta.productos ?? [];
    mensaje.acciones = (respuesta.acciones ?? []).map(accion => ({ ...accion, estado: 'pendiente' as const }));
    delete mensaje.enCurso;
    delete mensaje.progreso;
    this.mensajes = [...this.mensajes];
    this.respondiendo = false;
    this.guardarHistorial();
    this.bajarAlFinal();
    this.enfocarEntrada();
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

  private clave(idUsuario: string | null, modo = this.modo): string {
    return `${CLAVE_HISTORIAL}.${modo}.${idUsuario ?? 'invitado'}`;
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

  private borrarHistorial(idUsuario: string, modo: 'tienda' | 'panel'): void {
    try {
      sessionStorage.removeItem(this.clave(idUsuario, modo));
    } catch {
      // Sin almacenamiento: no hay nada que borrar.
    }
  }

  private guardarHistorial(): void {
    try {
      // Una respuesta a medias no se guarda: si se recarga la página, la pregunta queda lista para reintentar.
      sessionStorage.setItem(this.clave(this.idUsuario), JSON.stringify(this.mensajes.filter(m => !m.enCurso)));
    } catch {
      // Sin almacenamiento (modo privado o bloqueado): la conversación solo dura mientras la página esté abierta.
    }
  }
}
