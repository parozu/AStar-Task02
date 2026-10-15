using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

public class PathMarker
{
    public MapLocation location;
    public float G;
    public float H;
    public float F;
    public GameObject marker;
    public PathMarker parent;


    public PathMarker(MapLocation l, float g, float h, GameObject marker, PathMarker p)
    {
        location = l;
        G = g;
        H = h;
        F = g + h;
        this.marker = marker;
        parent = p;
    }
    public PathMarker(MapLocation l, float g, float h, float f, GameObject marker, PathMarker p)
    {
        location = l;
        G = g;
        H = h;
        F = f;
        this.marker = marker;
        parent = p;
    }

    public override bool Equals(object obj)
    {
        if ((obj == null) || !this.GetType().Equals(obj.GetType()))
        {
            return false;
        }
        else
        {
            return location.Equals(((PathMarker) obj).location);
        }
    }

    public override int GetHashCode()
    {
        return 0;
    }
}


public class FindPathAStar : MonoBehaviour
{
    public Maze maze;
    public Material closedMaterial;
    public Material openMaterial;

    List<PathMarker> open = new List<PathMarker>();   //Lista OPEN de PathMakers
    List<PathMarker> closed = new List<PathMarker>(); //Lista CLOSED de PathMakers

    public GameObject start;
    public GameObject end;
    public GameObject pathP;

    public float updateFreq = 0.2f;

    PathMarker goalNode;  //PathMaker que marca la posicion final (DESTINO)
    PathMarker startNode; //PathMaker que marca la posicion inicial (ORIGEN)

    PathMarker lastPos; //Nodo en el que estamos en cada momento, cuando estamos
                        //buscando el DESTINO y cuando estamos dibujando el CAMINO
    bool done = false;  //Booleano que se utiliza para decir si se ha alcanzado el objetivo

    bool algorithmStarted = false;  //Hemos inicializado el juego

    float pathDelay = 3f;

    void RemoveAllMarkers()
    {
        //TODO: Eliminar todos los GameObjects que puedan haber en el mapa (maze)
        GameObject[] markers = GameObject.FindGameObjectsWithTag("marker");
        foreach (GameObject m in markers)
            Destroy(m);
    }

    void BeginSearch()
    {
        CancelInvoke(nameof(Search));
        //Aqui inicializamos las posiciones inicial, final y las listas OPEN y CLOSED

        //Esta variable controlara si el problema esta solucionado
        done = false;
        //Eliminar posibles GameObjects en el mapa
        RemoveAllMarkers();

        algorithmStarted = true;

        //Creamos una lista de localizaciones validas, esto es, objetos del tipo MapLocation
        List<MapLocation> locations = new List<MapLocation>();
        //Ahora generamos el grafo comprobando si, en cada posicion, tenemos un muro o no (-1 o 1)
        // En el caso de no tener un muro, agregamos un nuevo MapLocation con esa coordenada a la lista
        for (int z = 1; z < maze.depth - 1; z++)
            for (int x = 1; x < maze.width - 1; x++)
            {
                if (maze.map[x, z] != 1)
                    locations.Add(new MapLocation(x, z));
            }
        //Las listas tienen un metodo Shuffle que nos permite barajar los elementos que contiene
        // Barajamos las posiciones para que no esten contiguas
        locations.Shuffle();

        //Escogemos una localizacion al azar (la que se queda en la primera posicion, por ejemplo) y esa sera
        // la posicion de salida, almacenada en un Vector3
        // Cuidado: Para mantener la ubicacion consistente, tenemos que multiplicar la coordenada por la escala del laberinto (maze.scale)
        Vector3 startLocation = new Vector3(locations[0].x * maze.scale, 0, locations[0].z * maze.scale);
        //Ahora podemos crear un nuevo PathMarker, el objeto que utitlizaremos para representar una posicion dentro del laberinto de forma visual
        //Un PathMarker necesita:
        // - MapLocation
        // - Los valores de la funcion f:
        //      - g(n)
        //      - h(n)
        // - El GameObject con el que representaremos esta posicion
        // - Y otro PathMarker que sera el "padre" de este nodo

        //Inicializamos goalNode como posicion de llegada, creando de la misma forma un Vector3 con las coordenadas, 
        // y despues un PathMarker con esta informacion
        Vector3 goalLocation = new Vector3(locations[1].x * maze.scale, 0, locations[1].z * maze.scale);
        goalNode = new PathMarker(new MapLocation(locations[1].x, locations[1].z), 0, 0, 0, Instantiate(end, goalLocation, Quaternion.identity), null);

        // Este nodo empieza con su valor de la heuristica (distancia euclidea hasta el objetivo).
        float h_n = Vector2.Distance(locations[0].ToVector(), goalNode.location.ToVector());
        GameObject startBlock = Instantiate(start, startLocation, Quaternion.identity);
        
        startNode = new PathMarker(locations[0], 0, h_n, startBlock, null);
        
        TextMesh[] values = startBlock.GetComponentsInChildren<TextMesh>();
        values[0].text = "g(n): " + startNode.G.ToString("0.00");
        values[1].text = "h(n): " + startNode.H.ToString("0.00");
        values[2].text = "f(n): " + startNode.F.ToString("0.00");
        

        //Eliminamos lo que contenga las listas de OPEN y CLOSED por si se ha vuelto a ejecutar el algoritmo
        open.Clear();
        closed.Clear();

        //Comenzamos añadiendo el nodo startNode a CLOSED
        closed.Add(startNode); 

        //Y para cuestiones relacionadas con la UI, almacenamos una referencia al nodo inicial en lastPos, por ahora
        lastPos = startNode;
    }

    //TODO: Completad este metodo
    void Search(PathMarker thisNode)
    {
        //Este metodo ejecutara la busqueda para encontrar el camino

        // Si el algortmo aun no ha comenzado, salimos sin hacer nada
        if (!algorithmStarted || done || thisNode == null) return;

        //Comprobamos si este nodo desde donde empezamos es ya solucion
        if (thisNode.Equals(goalNode))
        {
            FinishSearch();
            return;
        }
        //Comenzamos buscando vecinos de este nodo
        // MapLocation almacena una coordenada x,z y tambien una lista de posibles direcciones (adelante/atras/izquierda/derecha)
        foreach(MapLocation dir in maze.directions)
        {
            //Inicializamos la localizacion "neighbour"
            //Ojo, en este punto neighbour es solo una posicion en el mapa
            MapLocation neighbour = dir + thisNode.location;

            //Antes de procesar este vecino chequeamos que cumpla ciertas condiciones:
            //Antes de procesarlo, nos preguntamos:
            //1.- ¿Es un muro? Si es asi, continuamos el bucle
            if (maze.map[neighbour.x, neighbour.z] == 1) continue;

            //2.- En esta direccion, ¿Sigo dentro del laberinto? Tienes que estar entre 1 y maze.width para el eje X o entre 1 y maze.depth para el eje Z
            if (neighbour.x < 1 || neighbour.x >= maze.width - 1 || neighbour.z < 1 || neighbour.z >= maze.depth - 1) continue;

            //3.- ¿He visitado ya al vecino en una iteracion anterior? o lo que es lo mismo, ¿Esta en CLOSED?
            if (IsClosed(neighbour)) continue;

            //Aqui ya sabemos que el vecino es valido
            //Sumamos la G que teniamos a la distancia entre los nodos vecinos
            float G = thisNode.G + Vector2.Distance(thisNode.location.ToVector(), neighbour.ToVector());

            //Creamos un gameObject "pathBlock" para poner en este punto de nuestro camino
            //"pathBlock" tiene el texto G, F, H adjunto como subcomponentes, los inicializamos esto strings
            //h(n): distancia desde el vecino hasta el destino
            float H = Vector2.Distance(neighbour.ToVector(), goalNode.location.ToVector());
            float F = G + H;

            //Ahora tenemos que comprobar si este nodo ya esta en la lista de OPEN
            // Si esta en la lista, actualizaremos sus valores para g(n) y h(n) , asi como el padre que sera este ahora
            // Si no esta lo añadiremos a la lista OPEN
            // Esta comprobacion la puede realizar la funcion "UpdateMarker"
            PathMarker inOpen = open.Find(p => p.location.Equals(neighbour));
            if (inOpen != null)
            {
                if (G < inOpen.G)
                {
                    UpdateMarker(neighbour, G, H, F, thisNode);
                    SetValues(inOpen.marker, G, H, F);
                }

                continue;
            }

            //Creamos un gameObject para poner en este punto nuestro camino
            Vector3 blockPos = new Vector3(neighbour.x * maze.scale, 0, neighbour.z * maze.scale);
            GameObject pathBlock = Instantiate(pathP, blockPos, Quaternion.identity);
            pathBlock.GetComponent<Renderer>().material = openMaterial;

            //Inicializamos el texto del GameObject
            SetValues(pathBlock, G, H, F);
            open.Add(new PathMarker(neighbour, G, H, F, pathBlock, thisNode));
        }

        //Si OPEN esta vacio, no hay camino posible hasta el destino
        if (open.Count == 0)
        {
            Debug.Log("No existe camino entre origen y destino");
            FinishSearch();
            return;
        }

        //Ahora tenemos que elegir el siguiente nodo a expandir
        // De open, nos quedamos con el que menor f(n) tenga
        PathMarker pm = open.OrderBy(p => p.F).ThenBy(p => p.H).First();

        // Ese nodo lo añadimos a CLOSED
        closed.Add(pm);
        // Lo quitamos de OPEN
        open.Remove(pm);
        
        // Indicamos, cambiando el material, que este marcador tendra el color de los puntos "CLOSED"
        pm.marker.GetComponent<Renderer>().material = closedMaterial;

        // Indicamos que este es el lastPos donde nos hemos quedado
        lastPos = pm;

        //Si el nodo que cerramos es el destino, finito
        if(pm.Equals(goalNode)) FinishSearch();
    }

    //Marca la busqueda como terminada y detiene la animacion
    void FinishSearch()
    {
        done = true;
        CancelInvoke(nameof(Search));

        if (lastPos != null && lastPos.Equals(goalNode)) Invoke(nameof(GetPath), pathDelay);
    }

    //Escribe en los hijos del marcador
    void SetValues(GameObject block, float g, float h, float f)
    {
        TextMesh[] values = block.GetComponentsInChildren<TextMesh>();
        values[0].text = "g(n): " + g.ToString("0.00");
        values[1].text = "h(n): " + h.ToString("0.00");
        values[2].text = "f(n): " + f.ToString("0.00");
    }

    bool UpdateMarker(MapLocation pos, float g, float h, float f, PathMarker prt)
    {
        //Para cada pathMaker en al open list
        //   Si pos esta en la lista:
        //      actualizamos G H F y parent
        //      devolvemos TRUE
        //Devolvemos FALSE
        foreach (PathMarker p in open)
        {
            if (p.location.Equals(pos))
            {
                p.G = g;
                p.H = h;
                p.F = f;
                p.parent = prt;
                return true;
            }
        }
        return false;
    }

    bool IsClosed(MapLocation marker)
    {
        //Devolvemos TRUE si marker esta en la lista de closed
        foreach(PathMarker p in closed)
        {
            if (p.location.Equals(marker)) return true;
        }
        return false;
    }


    // Start is called before the first frame update
    void Start()
    {
        BeginSearch();
    }



    //TODO: Completad este metodo
    void GetPath()
    {
        //Este metodo dibujara el camino desde el inicio hasta el destino
        //Si la búsqueda ha terminado llegando al destino
        if (!done || lastPos == null || !lastPos.Equals(goalNode)) return;

        //Borrar todos los pathMakers
        RemoveAllMarkers();

        //Inicializamos begin a lastPos
        PathMarker begin = lastPos;

        //Mientras startNode no sea begin y begin no sea NULL
        while(!startNode.Equals(begin) && begin != null)
        {
            //Instanciamos un nuevo pathMaker que senyalara el camino de vuelta
            Vector3 pos = new Vector3(begin.location.x * maze.scale, 0, begin.location.z * maze.scale);
            GameObject block = Instantiate(pathP, pos, Quaternion.identity);
            block.GetComponent<Renderer>().material = openMaterial;
            SetValues(block, begin.G, begin.H, begin.F);
            //begin pasara ahora a señalar al su nodo padre
            begin = begin.parent;
        }

        //Instanciamos un ultimo pathMaker que senyalara la posicion inicial
        Vector3 startPos = new Vector3(startNode.location.x * maze.scale, 0, startNode.location.z * maze.scale);
        GameObject startBlock = Instantiate(start, startPos, Quaternion.identity);
        SetValues(startBlock, startNode.G, startNode.H, startNode.F);

        //RemoveAllMarkers también borró el marcador del destino, lo volvemos a poner
        Vector3 goalPos = new Vector3(goalNode.location.x * maze.scale, 0, goalNode.location.z * maze.scale);
        Instantiate(end, goalPos, Quaternion.identity);

        CancelInvoke(nameof(GetPath));
    }

    void Search()
    {
        Search(lastPos);
    }

    // Update is called once per frame
    void Update()
    {
        //Si presionamos la letra "P" inicializamos el juego
        if (Keyboard.current[Key.P].wasPressedThisFrame)
        {
            BeginSearch();
        }

        //Si presionamos la letra "C" calculamos el siguiente movimiento del alg. A*
        if (Keyboard.current[Key.C].wasPressedThisFrame && !done)
        {
            Search(lastPos);
        }
        //Si presionamos la letra "M" dejamos solo los pathMakers que forman el path optimo
        if (Keyboard.current[Key.M].wasPressedThisFrame)
        {
            GetPath();
        }

        //Si presionamos la tecla A, se reproduce la busqueda a modo animacion con una frecuencia dada
        if (Keyboard.current[Key.A].wasPressedThisFrame)
        {
            InvokeRepeating(nameof(Search), 1.0f,updateFreq);
        }
    }
}