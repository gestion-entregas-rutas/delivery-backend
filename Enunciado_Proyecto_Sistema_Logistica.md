## Sistema de Gestión de Entregas y Rutas

("Logística de Última Milla")

Materia: Gestión de Pruebas e Implantación de Software

## 1. Contexto y problema a resolver

Un negocio que realiza entregas a domicilio (regalos, comida, insumos, o cualquier producto físico) necesita organizar cómo sus repartidores llevan los pedidos a los clientes. El reto no es solo "registrar pedidos", sino decidir de forma eficiente qué repartidor lleva qué pedido y en qué orden, considerando que:

- Los clientes hacen sus pedidos con anticipación (a veces días o semanas antes), indicando la fecha, la hora aproximada y la dirección de entrega.

- El negocio sigue recibiendo pedidos nuevos incluso el mismo día en que hay entregas programadas.

- Los pedidos que van hacia zonas cercanas conviene agruparlos en una sola ruta para un mismo repartidor (hasta un máximo de pedidos por ruta, por ejemplo 4), mientras que zonas con pocos pedidos pueden despacharse con rutas más pequeñas (1 o 2 pedidos).

- Un repartidor no puede llevar pedidos indefinidamente, tiene un límite de capacidad por salida.

Este es el mismo problema que enfrenta cualquier negocio real de entregas (floristerías, tiendas de regalos, restaurantes, farmacias) en fechas de alta demanda, como el Día de San Valentín, Navidad o el Día de la Madre, donde el volumen de pedidos programados con anticipación se dispara y el sistema debe organizar correctamente cientos o miles de entregas para un mismo día.

## 2. Objetivo del sistema

Desarrollar un sistema que permita:

- 1. Registrar pedidos con su fecha, hora y dirección de entrega deseada.

- 2. Agrupar automáticamente los pedidos por zona geográfica y por fecha de entrega.

- 3. Armar rutas de entrega para cada repartidor, respetando el límite máximo de pedidos por ruta.

- 4. Permitir que sigan ingresando pedidos nuevos el mismo día de la entrega, insertándolos en una ruta existente si hay espacio, o generando una ruta nueva si no lo hay.

- 5. Dar seguimiento en tiempo real al estado de cada pedido (pendiente, asignado a ruta, en camino, entregado).

## 3. Módulos funcionales mínimos

- 1. Registro de pedidos: el cliente (o el personal del negocio en su nombre) ingresa el pedido, la dirección, la fecha y la hora de entrega deseada.

- 2. Gestión de zonas: el negocio define las zonas de reparto de la ciudad, para que el sistema pueda agrupar pedidos cercanos entre sí.

- 3. Armado de rutas: el sistema agrupa automáticamente los pedidos pendientes de una fecha y zona, respetando el máximo de pedidos por ruta, y genera las rutas a asignar a los repartidores disponibles.

- 4. Gestión de repartidores: alta de repartidores, disponibilidad, y visualización de su ruta asignada.

- 5. Seguimiento de entregas: actualización del estado del pedido conforme el repartidor avanza (recogido, en camino, entregado), visible tanto para el negocio como, opcionalmente, para el cliente.

## 4. Componentes del sistema


Aplicación web: para uso del personal del negocio. Permite registrar pedidos, definir zonas, ver las rutas armadas, y hacer ajustes manuales si algo no se asignó como se esperaba.

Aplicación móvil: para uso de los repartidores. Permite ver la ruta asignada, el orden de entregas, y marcar cada pedido como entregado.

Ambas aplicaciones deben conectarse al mismo sistema central, de forma que un cambio de estado hecho desde la app móvil del repartidor (por ejemplo, marcar un pedido como entregado) se refleje de inmediato en la aplicación web del negocio.

## 5. Consideraciones importantes para el diseño

- El sistema debe manejar bien la diferencia entre "cuándo se registra el pedido" (que puede ser mucho antes) y "cuándo se despacha" (el día de la entrega).

- Debe existir un mecanismo (puede ser un proceso programado o una acción manual del negocio) que dispare el armado de rutas para una fecha determinada, agrupando todos los pedidos pendientes de esa fecha y zona.

- El sistema debe decidir correctamente qué hacer cuando llega un pedido de último momento: si cabe en una ruta ya armada, debe insertarse ahí; si no cabe, debe generarse una ruta nueva o esperar según corresponda.

- Se debe evitar que dos pedidos terminen asignados de forma incorrecta al mismo repartidor al mismo tiempo cuando hay muchas solicitudes entrando de forma simultánea

## 6. Algoritmos que se pueden investigar para el armado de rutas

Algunas opciones a investigar:

- Asignación por cercanía (vecino más cercano).

- Asignación por puntaje ponderado (combinando distancia, carga actual del repartidor y prioridad del pedido).

- Problema de asignación / algoritmo húngaro (para optimizar la asignación de un lote completo de pedidos a los repartidores disponibles).

- Heurísticas de inserción para rutas con múltiples paradas (usadas en el clásico "problema de ruteo de vehículos" o VRP).

- Variantes con capacidad limitada y ventanas de tiempo (Capacitated VRP, VRP with Time Windows), que son las que más se acercan al escenario descrito en este enunciado.

- Agrupación previa por zona antes de armar la ruta (clusterización geográfica), como paso inicial antes de aplicar cualquiera de los algoritmos anteriores.

Se recomienda documentar en el informe final qué algoritmo se eligió y por qué, sin que esto sea el foco central de la evaluación.

## 7. Plan de pruebas a aplicar

El sistema debe someterse a las siguientes pruebas, cubriendo tanto la aplicación web como la móvil donde aplique:

- Análisis estático de código.

- Pruebas unitarias.

- Pruebas de integración.

- Pruebas del sistema.

- Pruebas de volumen.

- Pruebas de carga.

- Pruebas de estrés.


Para las pruebas de volumen, carga y estrés, es indispensable que el equipo pueble el sistema con una cantidad significativa de datos (pedidos, repartidores, zonas, rutas) antes de ejecutar las pruebas, de manera que los resultados reflejen un escenario realista y no un sistema prácticamente vacío. Se sugiere simular un escenario de alta demanda (por ejemplo, la cantidad de pedidos que recibiría el negocio en una fecha de alto movimiento, como el Día de San Valentín) para dar sentido real a estas pruebas.

## 8. Entregables

- 1. Código fuente de la aplicación web y móvil.

- 2. Documento con el diseño del sistema (módulos, flujo de datos, algoritmo elegido para el armado de rutas).

- 3. Evidencia de análisis estático de código y de las correcciones aplicadas.

- 4. Suite de pruebas unitarias, de integración y del sistema, con sus resultados.

- 5. Informe de pruebas de volumen, carga y estrés, incluyendo la descripción de cómo se pobló el sistema para dichas pruebas y los resultados obtenidos.

- 6. Informe final que relacione los hallazgos de las pruebas con posibles mejoras al sistema.
