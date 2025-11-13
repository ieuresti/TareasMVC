function agregarNuevaTareaAlListado() {
    tareaListadoViewModel.tareas.push(new tareaElementoListadoViewModel({ id: 0, titulo: '' }));

    $("[name=titulo-tarea]").last().focus();
}

async function obtenerTareas() {
    tareaListadoViewModel.cargando(true);

    const respuesta = await fetch(urlTareas, {
        method: 'GET',
        headers: {
            'Content-Type': 'application/json'
        }
    });
    if (!respuesta.ok) {
        manejarErrorApi(respuesta);
        return;
    }
    const json = await respuesta.json();
    tareaListadoViewModel.tareas([]);
    json.forEach(valor => {
        tareaListadoViewModel.tareas.push(new tareaElementoListadoViewModel(valor));
    });

    tareaListadoViewModel.cargando(false);
}

async function manejarFocusoutTituloTarea(tarea) {
    const titulo = tarea.titulo();
    if (!titulo) {
        tareaListadoViewModel.tareas.pop();
        return;
    }
    const data = JSON.stringify(titulo);
    const respuesta = await fetch(urlTareas, {
        method: 'POST',
        body: data,
        headers: {
            'Content-Type': 'application/json'
        }
    });

    if (respuesta.ok) {
        const json = await respuesta.json();
        tarea.id(json.id);
    } else {
        manejarErrorApi(respuesta);
    }
}

async function actualizarOrdenTareas() {
    const ids = obtenerIdsTareas();
    await enviarIdsTareasAlBackend(ids);

    // Crear un nuevo array con las tareas ordenadas según el orden proporcionado en `ids`.
    const arregloOrdenado = tareaListadoViewModel.tareas().slice().sort(function (a, b) {
        // Comparador para sort:
        // - a.id() y b.id() obtienen el id de cada elemento (probablemente observables), lo convertimos a string
        //   porque `ids` contiene strings.
        // - ids.indexOf(...) devuelve la posición del id en el array `ids`.
        // - Restando las posiciones obtenemos:
        //     <0 => a debe ir antes que b
        //      0 => mantienen el mismo orden relativo
        //     >0 => a debe ir después que b
        return ids.indexOf(a.id().toString()) - ids.indexOf(b.id().toString());
    });

    tareaListadoViewModel.tareas([]);
    tareaListadoViewModel.tareas(arregloOrdenado);
}

function obtenerIdsTareas() {
    // Selecciona todos los elementos del DOM que tengan el atributo name="titulo-tarea".
    const ids = $("[name=titulo-tarea]")
        // map() de jQuery itera sobre cada elemento de la colección.
        // La función se ejecuta con `this` apuntando al elemento DOM actual.
        .map(function () {
            // $(this) envuelve el elemento DOM en un objeto jQuery para usar sus métodos.
            // attr("data-id") lee el atributo HTML `data-id` del elemento.
            // Si no existe el atributo, devuelve `undefined`.
            return $(this).attr("data-id");
        })
        // .get() convierte el resultado de map (objeto jQuery) en un Array nativo de JavaScript.
        .get();
    // Devuelve el array de ids (normalmente strings). Si no hay elementos, devuelve [].
    return ids;
}

async function enviarIdsTareasAlBackend(ids) {
    var data = JSON.stringify(ids);
    await fetch(`${urlTareas}/ordenar`, {
        method: 'POST',
        body: data,
        headers: {
            'Content-Type': 'application/json'
        }
    });
}

async function manejarClickTarea(tarea) {
    if (tarea.esNuevo()) {
        return;
    }
    const respuesta = await fetch(`${urlTareas}/${tarea.id()}`, {
        method: 'GET',
        headers: {
            'Content-Type': 'application/json'
        }
    });
    if (!respuesta.ok) {
        manejarErrorApi(respuesta);
        return;
    }
    const json = await respuesta.json();

    tareaEditarVM.id = json.id;
    tareaEditarVM.titulo(json.titulo);
    tareaEditarVM.descripcion(json.descripcion);
    // Limpiar arreglo pasos para evitar inserciones al consultar
    tareaEditarVM.pasos([]);

    json.pasos.forEach(paso => {
        tareaEditarVM.pasos.push(
            new pasoViewModel({...paso, modoEdicion: false})
        )
    });

    modalEditarBootstrap.show();
}

async function manejarCambioEditarTarea() {
    const obj = {
        id: tareaEditarVM.id,
        titulo: tareaEditarVM.titulo(),
        descripcion: tareaEditarVM.descripcion()
    };
    if (!obj.titulo) {
        return;
    }
    await editarTareaCompleta(obj);

    // Buscar el índice en el array (observable) `tareas()` cuya tarea tenga el id igual a `obj.id`.
    // findIndex devuelve -1 si no encuentra coincidencia.
    const indice = tareaListadoViewModel.tareas().findIndex(t => t.id() === obj.id);
    // Obtener la tarea del array usando el índice encontrado.
    const tarea = tareaListadoViewModel.tareas()[indice];
    // Actualizar el título de la tarea. Al modificarlo, la UI se actualizará automáticamente.
    tarea.titulo(obj.titulo);
    modalOperacionExitosa();
}

async function editarTareaCompleta(tarea) {
    const data = JSON.stringify(tarea);
    const respuesta = await fetch(`${urlTareas}/${tarea.id}`, {
        method: 'PUT',
        body: data,
        headers: {
            'Content-Type': 'application/json'
        }
    });
    if (!respuesta.ok) {
        manejarErrorApi(respuesta);
        throw "error";
    }
}

function intentarBorrarTarea(tarea) {
    modalEditarBootstrap.hide();

    confirmarAccion({
        callbackAceptar: () => {
            borrarTarea(tarea);
        },
        callbackCancelar: () => {
            modalEditarBootstrap.show();
        },
        titulo: `¿Desea borrar la tarea ${tarea.titulo()}?`
    })
}

async function borrarTarea(tarea) {
    const idTarea = tarea.id;

    const respuesta = await fetch(`${urlTareas}/${idTarea}`, {
        method: 'DELETE',
        headers: {
            'Content-Type': 'application/json'
        }
    });

    if (respuesta.ok) {
        const indice = obtenerIndiceTareaEnEdicion();
        tareaListadoViewModel.tareas.splice(indice, 1);
    }
}

function obtenerIndiceTareaEnEdicion() {
    return tareaListadoViewModel.tareas().findIndex(t => t.id() == tareaEditarVM.id);
}

function obtenerTareaEnEdicion() {
    const indice = obtenerIndiceTareaEnEdicion();
    return tareaListadoViewModel.tareas()[indice];
}

$(function () {
    $("#reordenable").sortable({
        // solamente se podra arrastrar de arriba hacia abajo
        axis: 'y',
        // esto se ejecutara cuando terminemos de arrastrar la tarea
        stop: async function () {
            await actualizarOrdenTareas();
        }
    });
});