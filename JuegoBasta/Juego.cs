using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace JuegoBasta
{
    public enum Comando { NombreEnviado, LetraEnviada, JugadaEnviada, JugadaConfirmada }
    public partial class Juego : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void ActualizarValor(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        void CambiarMensaje(string msj)
        {
            currentDispatcher.Invoke(new Action(() =>
            {

                Mensaje = msj;
                ActualizarValor();
            }));
        }


        public int pAnimal1 { get; set; }
        public int pAnimal2 { get; set; }

        public int pNombre1 { get; set; }
        public int pNombre2 { get; set; }

        public int pLugar1 { get; set; }
        public int pLugar2 { get; set; }

        public int pColor1 { get; set; }
        public int pColor2 { get; set; }
        public int pComida1 { get; set; }
        public int pComida2 { get; set; }

        public string Jugador1 { get; set; } = "User24";
        public string Jugador2 { get; set; }
        public string IP { get; set; } = "localhost";
        public string Mensaje { get; set; }
        public bool MainVisible { get; set; } = true;
        public ICommand IniciarCommand { get; set; }
        public ICommand JugarCommand { get; set; }
        public int PuntajeJugador1 { get; set; } = 0;
        public int PuntajeJugador2 { get; set; } = 0;
        Random r = new Random();
        public char Letra { get; set; }

        public Respuestas RespuestaJugador1 { get; set; } = new Respuestas();
        public Respuestas RespuestaJugador2 { get; set; } = new Respuestas();

        public bool PuedeJugarCliente { get; set; }
        public bool PuedeJugarServidor { get; set; }
        public bool ConfirmacionServer { get; set; } = false;
        public bool ConfirmacionCliente { get; set; } = false;

        HttpListener servidor;
        ClientWebSocket cliente;
        Dispatcher currentDispatcher;
        WebSocket webSocket;

        public Juego()
        {
            currentDispatcher = Dispatcher.CurrentDispatcher;
            IniciarCommand = new RelayCommand<bool>(IniciarPartida);
            JugarCommand = new RelayCommand<Respuestas>(Jugar);
        }
        public char ElegirLetra()
        {
            return (char)r.Next('A', 'Z');
        }

        // SI CREA PARTIDA SE INICIA COMO SERVIDOR
        public void CrearPartida()
        {
            servidor = new HttpListener();
            servidor.Prefixes.Add("http://*:8080/basta/");
            servidor.Start();
            servidor.BeginGetContext(OnContext, null);

            Mensaje = "Esperando adversario...";
            ActualizarValor();
        }
        // SI SE UNE A UNA PARTIDA INICIA COMO CLIENTE
        public async Task ConectarPartida()
        {
            cliente = new ClientWebSocket();
            await cliente.ConnectAsync(new Uri($"ws://{IP}:8080/basta/"), CancellationToken.None);
            webSocket = cliente;
            // EL CLIENTE RECIBE EL NOMBRE DEL SERVIDOR
            RecibirComando();
        }
        // CUANDO RECIBE UNA CONEXIÓN CON EL CLIENTE
        private async void OnContext(IAsyncResult ar)
        {
            var context = servidor.EndGetContext(ar);

            if (context.Request.IsWebSocketRequest)
            {
                var listener = await context.AcceptWebSocketAsync(null);
                webSocket = listener.WebSocket;
                CambiarMensaje("Conexión exitosa. Esperando información del contrincante.");
                // EL SERVIDOR ENVÍA SU NOMBRE
                EnviarComando(new DatoEnviado { Comando = Comando.NombreEnviado, Nombre = Jugador1 });
                // EL SERVIDOR RECIBE EL COMANDO, QUE SERÍA EL NOMBRE DEL CLIENTE.
                RecibirComando();
            }
            else
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                servidor.BeginGetContext(OnContext, null);
            }
        }

        LobbyWindow lobby;
        // CUANDO YA SE INICIÓ, PERO AÚN NO SE CONECTAN EL SERVIDOR Y EL CLIENTE
        private async void IniciarPartida(bool tipoP)
        {
            try
            {
                MainVisible = false;
                lobby = new LobbyWindow();
                lobby.Closing += Lobby_Closing;
                lobby.DataContext = this;
                lobby.Show();
                ActualizarValor();

                // SI ES SERVIDOR, CREA LA PARTIDA
                if (tipoP == true)
                {
                    CrearPartida();
                }
                else // SI ES CLIENTE
                {
                    Jugador2 = Jugador1;
                    Jugador1 = null;
                    Mensaje = "Intentando conectar con el contrincante en " + IP;
                    ActualizarValor("Mensaje");
                    await ConectarPartida();
                }

            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
                ActualizarValor();
            }

        }

        private void Lobby_Closing(object sender, CancelEventArgs e)
        {
            MainVisible = true;
            ActualizarValor("MainVisible");
            if (servidor != null)
            {
                servidor.Stop();
                servidor = null;
            }
        }

        public async void EnviarComando(DatoEnviado datos)
        {
            byte[] buffer;
            var json = JsonConvert.SerializeObject(datos);
            buffer = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        JuegoBastaWindow ventanaJuego;
        public string BotonCliente { get; set; } = "Hidden";
        public string BotonServer { get; set; } = "Hidden";
        private async void RecibirComando()
        {
            try
            {
                byte[] buffer = new byte[1024];
                while (webSocket.State == WebSocketState.Open)
                {
                    var resultado = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (resultado.MessageType == WebSocketMessageType.Close)
                    {
                        ventanaJuego.Close();
                        return;
                    }

                    string datosrecibidos = Encoding.UTF8.GetString(buffer, 0, resultado.Count);
                    var comandorecibido = JsonConvert.DeserializeObject<DatoEnviado>(datosrecibidos);

                    // YA ESTÁN CONECTADOS EL CLIENTE Y EL SERVIDOR. SE ABRE LA VENTANA DEL JUEGO.

                    if (cliente != null) // ES CLIENTE
                    {
                        switch (comandorecibido.Comando)
                        {
                            case Comando.NombreEnviado:
                                Jugador1 = (string)comandorecibido.Nombre;
                                CambiarMensaje("Se ha conectado con el jugador " + Jugador1);
                                _ = currentDispatcher.BeginInvoke(new Action(() =>
                                {
                                    // EL CLIENTE ENVÍA EL NOMBRE.
                                    EnviarComando(new DatoEnviado { Comando = Comando.NombreEnviado, Nombre = Jugador2 });
                                    lobby.Hide();

                                    BotonCliente = "Visible";
                                    ventanaJuego = new JuegoBastaWindow();
                                    ventanaJuego.Title = "Cliente";
                                    ventanaJuego.DataContext = this;

                                    PuedeJugarCliente = true;
                                    PuedeJugarServidor = false;
                                    CambiarMensaje("Inicie juego");
                                    ventanaJuego.ShowDialog();
                                    lobby.Show();

                                }));

                                break;

                            case Comando.JugadaEnviada:
                                currentDispatcher.Invoke(new Action(() =>
                                {
                                    RespuestaJugador1 = (Respuestas)comandorecibido.DatoRespuestas;
                                    CambiarMensaje("Respuestas recibidas");
                                    ConfirmacionCliente = true;
                                    EnviarComando(new DatoEnviado { Comando = Comando.JugadaConfirmada, ConfirmarJugada = ConfirmacionCliente });
                                    ActualizarValor();
                                }));
                                _ = ValidarRespuestas();
                                break;

                            case Comando.LetraEnviada:
                                Letra = (char)comandorecibido.LetraRandom;
                                ActualizarValor();
                                break;

                            case Comando.JugadaConfirmada:
                                ConfirmacionServer = (bool)comandorecibido.ConfirmarJugada;
                                _ = ValidarRespuestas();
                                break;
                        }
                    }
                    else // SERVIDOR
                    {
                        switch (comandorecibido.Comando)
                        {
                            case Comando.NombreEnviado:
                                Jugador2 = (string)comandorecibido.Nombre;
                                CambiarMensaje("Se ha conectado con el jugador " + Jugador2);

                                _ = currentDispatcher.BeginInvoke(new Action(() =>
                                {
                                    lobby.Hide();
                                    BotonServer = "Visible";
                                    JuegoBastaWindow ventanaJuego = new JuegoBastaWindow();
                                    ventanaJuego.Title = "Servidor";
                                    ventanaJuego.DataContext = this;

                                    Letra = ElegirLetra();
                                    EnviarComando(new DatoEnviado { Comando = Comando.LetraEnviada, LetraRandom = Letra });

                                    PuedeJugarCliente = false;
                                    PuedeJugarServidor = true;
                                    CambiarMensaje("Inicie juego");
                                    ventanaJuego.ShowDialog();
                                    lobby.Show();
                                }));

                                break;

                            case Comando.JugadaEnviada:
                                currentDispatcher.Invoke(new Action(() =>
                                {
                                    RespuestaJugador2 = (Respuestas)comandorecibido.DatoRespuestas;
                                    ConfirmacionServer = true;
                                    EnviarComando(new DatoEnviado { Comando = Comando.JugadaConfirmada, ConfirmarJugada = ConfirmacionServer });
                                    CambiarMensaje("Respuestas recibidas");
                                    ActualizarValor();
                                }));
                                _ = ValidarRespuestas();
                                break;

                            case Comando.JugadaConfirmada:
                                ConfirmacionCliente = (bool)comandorecibido.ConfirmarJugada;
                                _ = ValidarRespuestas();
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (webSocket.State == WebSocketState.Aborted)
                {
                    //ventanaJuego.Close();
                    //lobby.Close();
                    MainVisible = true;
                    ActualizarValor("MainVisible");
                }
                else
                {
                    CambiarMensaje(ex.Message);
                }
            }

        }

        private void Jugar(Respuestas obj)
        {


            if (cliente != null)//Esta jugando el cliente.
            {
                RespuestaJugador2 = obj;

                EnviarComando(new DatoEnviado { Comando = Comando.JugadaEnviada, DatoRespuestas = RespuestaJugador2 });

                PuedeJugarCliente = false;
                ActualizarValor();
                CambiarMensaje("Respuesta enviada");


            }
            else //Juega un servidor
            {
                RespuestaJugador1 = obj;

                EnviarComando(new DatoEnviado { Comando = Comando.JugadaEnviada, DatoRespuestas = RespuestaJugador1 });

                PuedeJugarServidor = false;
                ActualizarValor();
                CambiarMensaje("Respuesta enviada");


            }
            _ = ValidarRespuestas();
        }

        public int puntaje1 { get; set; }
        public int puntaje2 { get; set; }

        public int puntaje1Ronda { get; set; }
        public int puntaje2Ronda { get; set; }

        async Task ValidarRespuestas()
        {            
            
            if (ConfirmacionServer == true && ConfirmacionCliente == true)
            {

                // VALIDACIONES NOMBRE
                if (RespuestaJugador1.Nombre != null && RespuestaJugador2.Nombre != null)
                {
                    if (RespuestaJugador1.Nombre.ToUpper().StartsWith(Letra.ToString()))
                    {
                        if (RespuestaJugador1.Nombre.ToUpper() == RespuestaJugador2.Nombre.ToUpper())
                        {
                            pNombre1 = 50;
                        }
                        else if (RespuestaJugador1.Nombre.ToUpper() != RespuestaJugador2.Nombre.ToUpper())
                        {
                            pNombre1 = 100;
                        }
                    }
                    else
                    {
                        pNombre1 = 0;
                    }

                    if (RespuestaJugador2.Nombre.ToUpper().StartsWith(Letra.ToString()))
                    {
                        if (RespuestaJugador2.Nombre.ToUpper() == RespuestaJugador1.Nombre.ToUpper())
                        {
                            pNombre2 = 50;
                        }
                        else if (RespuestaJugador2.Nombre.ToUpper() != RespuestaJugador1.Nombre.ToUpper())
                        {
                            pNombre2 = 100;
                        }
                    }
                    else
                    {
                        pNombre2 = 0;
                    }

                }
                else if (RespuestaJugador1.Nombre == null && RespuestaJugador2.Nombre != null)//Que una llegue vacía la del Jugador 1 y la del Jugador 2 no
                {
                    if (RespuestaJugador2.Nombre.ToUpper().StartsWith(Letra.ToString()))
                    {
                        pNombre2 = 100;
                    }
                    else
                    {
                        pNombre2 = 0;
                    }
                }
                else if (RespuestaJugador2.Nombre == null && RespuestaJugador1.Nombre != null)//Que llegue vacía la del Jugador 2 y la del Jugador 1 no
                {
                    if (RespuestaJugador1.Nombre.ToUpper().StartsWith(Letra.ToString()))
                    {
                        pNombre1= 100;
                    }
                    else
                    {
                        pNombre1 = 0;
                    }
                }
                else//Las dos son nulas
                {
                    pNombre1 = 0;
                    pNombre2 = 0;
                }

                // VALIDACIONES LUGAR
                if (RespuestaJugador1.Lugar != null && RespuestaJugador2.Lugar != null)
                {
                    if (RespuestaJugador1.Lugar.ToUpper().StartsWith(Letra.ToString()))
                    {
                        if (RespuestaJugador1.Lugar.ToUpper() == RespuestaJugador2.Lugar.ToUpper())
                        {
                            pLugar1 = 50;
                        }
                        else if (RespuestaJugador1.Lugar.ToUpper() != RespuestaJugador2.Lugar.ToUpper())
                        {
                            pLugar1 = 100;
                        }
                    }
                    else
                    {
                        pLugar1 = 0;
                    }

                    if (RespuestaJugador2.Lugar.ToUpper().StartsWith(Letra.ToString()))
                    {
                        if (RespuestaJugador2.Lugar.ToUpper() == RespuestaJugador1.Lugar.ToUpper())
                        {
                            pLugar2 = 50;
                        }
                        else if (RespuestaJugador2.Lugar.ToUpper() != RespuestaJugador1.Lugar.ToUpper())
                        {
                            pLugar2 = 100;
                        }
                    }
                    else
                    {
                        pLugar2 = 0;
                    }

                }
                else if (RespuestaJugador1.Lugar == null && RespuestaJugador2.Lugar != null)//Que una llegue vacía la del Jugador 1 y la del Jugador 2 no
                {
                    if (RespuestaJugador2.Lugar.ToUpper().StartsWith(Letra.ToString()))
                    {
                        pLugar2 = 100;
                    }
                    else
                    {
                        pLugar2 = 0;
                    }
                }
                else if (RespuestaJugador2.Lugar == null && RespuestaJugador1.Lugar != null)//Que llegue vacía la del Jugador 2 y la del Jugador 1 no
                {
                    if (RespuestaJugador1.Lugar.ToUpper().StartsWith(Letra.ToString()))
                    {
                        pLugar1 = 100;
                    }
                    else
                    {
                        pLugar1 = 0;
                    }
                }
                else//Las dos son nulas
                {
                    pLugar1 = 0;
                    pLugar2 = 0;
                }


                // VALIDACIONES ANIMAL
                if (RespuestaJugador1.Animal != null && RespuestaJugador2.Animal != null)
                {
                    if (RespuestaJugador1.Animal.ToUpper().StartsWith(Letra.ToString()))
                    {
                        if (RespuestaJugador1.Animal.ToUpper() == RespuestaJugador2.Animal.ToUpper())
                        {
                            pAnimal1 = 50;
                        }
                        else if (RespuestaJugador1.Animal.ToUpper() != RespuestaJugador2.Animal.ToUpper())
                        {
                            pAnimal1 = 100;
                        }
                    }
                    else
                    {
                        pAnimal1 = 0;
                    }

                    if (RespuestaJugador2.Animal.ToUpper().StartsWith(Letra.ToString()))
                    {
                        if (RespuestaJugador2.Animal.ToUpper() == RespuestaJugador1.Animal.ToUpper())
                        {
                            pAnimal2 = 50;
                        }
                        else if (RespuestaJugador2.Animal.ToUpper() != RespuestaJugador1.Animal.ToUpper())
                        {
                            pAnimal2 = 100;
                        }
                    }
                    else
                    {
                        pAnimal2 = 0;
                    }

                }
                else if (RespuestaJugador1.Animal == null && RespuestaJugador2.Animal != null)//Que una llegue vacía la del Jugador 1 y la del Jugador 2 no
                {
                    if (RespuestaJugador2.Animal.ToUpper().StartsWith(Letra.ToString()))
                    {
                        pAnimal2 = 100;
                    }
                    else
                    {
                        pAnimal2 = 0;
                    }
                }
                else if (RespuestaJugador2.Animal == null && RespuestaJugador1.Animal != null)//Que llegue vacía la del Jugador 2 y la del Jugador 1 no
                {
                    if (RespuestaJugador1.Animal.ToUpper().StartsWith(Letra.ToString()))
                    {
                        pAnimal1 = 100;
                    }
                    else
                    {
                        pAnimal1 = 0;
                    }
                }
                else//Las dos son nulas
                {
                    pAnimal1 = 0;
                    pAnimal2 = 0;
                }


                // VALIDACIONES COLOR
                if (RespuestaJugador1.Color != null && RespuestaJugador2.Color != null)
                {
                    if (RespuestaJugador1.Color.ToUpper().StartsWith(Letra.ToString()))
                    {
                        if (RespuestaJugador1.Color.ToUpper() == RespuestaJugador2.Color.ToUpper())
                        {
                            pColor1 = 50;
                        }
                        else if (RespuestaJugador1.Color.ToUpper() != RespuestaJugador2.Color.ToUpper())
                        {
                            pColor1 = 100;
                        }
                    }
                    else
                    {
                        pColor1 = 0;
                    }

                    if (RespuestaJugador2.Color.ToUpper().StartsWith(Letra.ToString()))
                    {
                        if (RespuestaJugador2.Color.ToUpper() == RespuestaJugador1.Color.ToUpper())
                        {
                            pColor2 = 50;
                        }
                        else if (RespuestaJugador2.Color.ToUpper() != RespuestaJugador1.Color.ToUpper())
                        {
                            pColor2 = 100;
                        }
                    }
                    else
                    {
                        pColor2 = 0;
                    }

                }
                else if (RespuestaJugador1.Color == null && RespuestaJugador2.Color != null)//Que una llegue vacía la del Jugador 1 y la del Jugador 2 no
                {
                    if (RespuestaJugador2.Color.ToUpper().StartsWith(Letra.ToString()))
                    {
                        pColor2 = 100;
                    }
                    else
                    {
                        pColor2 = 0;
                    }
                }
                else if (RespuestaJugador2.Color == null && RespuestaJugador1.Color != null)//Que llegue vacía la del Jugador 2 y la del Jugador 1 no
                {
                    if (RespuestaJugador1.Color.ToUpper().StartsWith(Letra.ToString()))
                    {
                        pColor1 = 100;
                    }
                    else
                    {
                        pColor1 = 0;
                    }
                }
                else//Las dos son nulas
                {
                    pColor1 = 0;
                    pColor2 = 0;
                }


                // VALIDACIONES COMIDA
                if (RespuestaJugador1.Comida != null && RespuestaJugador2.Comida != null)
                {
                    if (RespuestaJugador1.Comida.ToUpper().StartsWith(Letra.ToString()))
                    {
                        if (RespuestaJugador1.Comida.ToUpper() == RespuestaJugador2.Comida.ToUpper())
                        {
                            pComida1 = 50;
                        }
                        else if (RespuestaJugador1.Comida.ToUpper() != RespuestaJugador2.Comida.ToUpper())
                        {
                            pComida1 = 100;
                        }
                    }
                    else
                    {
                        pComida1 = 0;
                    }

                    if (RespuestaJugador2.Comida.ToUpper().StartsWith(Letra.ToString()))
                    {
                        if (RespuestaJugador2.Comida.ToUpper() == RespuestaJugador1.Comida.ToUpper())
                        {
                            pComida2 = 50;
                        }
                        else if (RespuestaJugador2.Comida.ToUpper() != RespuestaJugador1.Comida.ToUpper())
                        {
                            pComida2 = 100;
                        }
                    }
                    else
                    {
                        pComida2 = 0;
                    }

                }
                else if (RespuestaJugador1.Comida == null && RespuestaJugador2.Comida != null)//Que una llegue vacía la del Jugador 1 y la del Jugador 2 no
                {
                    if (RespuestaJugador2.Comida.ToUpper().StartsWith(Letra.ToString()))
                    {
                        pComida2 = 100;
                    }
                    else
                    {
                        pComida2 = 0;
                    }
                }
                else if (RespuestaJugador2.Comida == null && RespuestaJugador1.Comida != null)//Que llegue vacía la del Jugador 2 y la del Jugador 1 no
                {
                    if (RespuestaJugador1.Comida.ToUpper().StartsWith(Letra.ToString()))
                    {
                        pComida1 = 100;
                    }
                    else
                    {
                        pComida1 = 0;
                    }
                }
                else//Las dos son nulas
                {
                    pComida1 = 0;
                    pComida2 = 0;
                }


                puntaje1Ronda= pNombre1 + pComida1 + pColor1 + pLugar1 + pAnimal1;
                puntaje2Ronda= pNombre2 + pComida2 + pColor2 + pLugar2 + pAnimal2;
                PuntajeJugador1 = PuntajeJugador1 + puntaje1Ronda;
                PuntajeJugador2 = PuntajeJugador2 + puntaje2Ronda;

                bool ganaJugador1 = (puntaje1Ronda > puntaje2Ronda);

                if (puntaje1Ronda == puntaje2Ronda)
                {
                    CambiarMensaje("Empate");
                    puntaje1++;
                    puntaje2++;
                }
                else if (ganaJugador1)
                {
                    CambiarMensaje($"Gana jugador {Jugador1}");
                    puntaje1++;
                    puntaje1Ronda = 0;
                    puntaje2Ronda = 0;
                }
                else
                {
                    CambiarMensaje($"Gana jugador {Jugador2}");
                    puntaje2++;
                    puntaje1Ronda = 0;
                    puntaje2Ronda = 0;
                }

                if (puntaje1 <= 2 && puntaje2 <= 2)
                {
                    await Task.Delay(10000);
                    CambiarMensaje("Esperando el siguiente turno");
                    await Task.Delay(10000);
                    RespuestaJugador1.Nombre = RespuestaJugador1.Lugar = RespuestaJugador1.Color = RespuestaJugador1.Comida = RespuestaJugador1.Animal = null;
                    RespuestaJugador2.Nombre = RespuestaJugador2.Lugar = RespuestaJugador2.Color = RespuestaJugador2.Comida = RespuestaJugador2.Animal = null;
                    pNombre1 = pAnimal1 = pColor1 = pComida1 = pLugar1 = 0;
                    pNombre2 = pAnimal2 = pColor2 = pComida2 = pLugar2 = 0;

                    if (cliente != null)
                    {
                        PuedeJugarCliente = true;
                    }
                    if (servidor != null)
                    {
                        PuedeJugarServidor = true;
                        Letra = ElegirLetra();
                        EnviarComando(new DatoEnviado { Comando = Comando.LetraEnviada, LetraRandom = Letra });
                    }

                    ConfirmacionServer = ConfirmacionCliente = false;

                    CambiarMensaje("Escribe tus respuestas");
                }
                else
                {
                    await Task.Delay(10000);
                    if(PuntajeJugador1==PuntajeJugador2)
                    {
                        CambiarMensaje("Juego terminado. Hay un empate" );
                    }
                    else
                    CambiarMensaje("Juego terminado. Gana " + ((PuntajeJugador1 > PuntajeJugador2) ? Jugador1 : Jugador2));
                }
            }
        }
        public class DatoEnviado
        {
            public Comando Comando { get; set; }
            public string Nombre { get; set; }
            public char LetraRandom { get; set; }
            public Respuestas DatoRespuestas { get; set; }

            public bool ConfirmarJugada { get; set; }
        }
    }
}