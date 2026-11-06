# Base de conocimiento del asistente

Documentos que el asistente (Andi) usa para responder. Se indexan en ChromaDB por secciones `##`.

- Formato: `.md` o `.txt`. La primera línea `# Título` es el nombre del documento (se muestra como fuente).
- Cada sección `##` debe entenderse sola: el asistente recibe fragmentos sueltos, no el documento completo.
- Las tablas markdown se convierten en frases al indexar ("Talla: M; Pecho: 96–100 cm.").
- Los comentarios `<!-- ... -->` **no se indexan**: úselos para notas internas.
- Este README no se indexa (`Rag:ExcludedFiles` en `asistente.json`).

Los valores de envío, reserva y carrito vienen de la configuración de los servicios (`docker-compose.yml`).
Si cambian allá, actualícelos acá.

Tras modificar los documentos: `POST /api/asistente/documentos/reindexar` con un token de administrador.
