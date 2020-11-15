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
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace JuegoBasta
{
    public enum Comando { NombreEnviado, LetraEnviada, JugadaEnviada }
    public class Juego : INotifyPropertyChanged
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

        public string Jugador1 { get; set; } = "Jugador";
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
            return (char)r.Next('a', 'z'); 
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

                                    // SE RECIBE LA LETRA
                                    //RecibirComando();
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
                                    ActualizarValor();
                                }));
                                break;



                            case Comando.LetraEnviada:
                                // LETRA NO TOMA EL VALOR DE DATO
                                Letra = (char)comandorecibido.LetraRandom;
                                ActualizarValor();
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
                                    CambiarMensaje("Respuestas recibidas");
                                    ActualizarValor();
                                }));
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
            if (cliente != null)
            {
                RespuestaJugador2 = obj;
                
                EnviarComando(new DatoEnviado { Comando = Comando.JugadaEnviada, DatoRespuestas = RespuestaJugador2 });
                ActualizarValor();
                PuedeJugarCliente = false;
                PuedeJugarServidor = false;
                CambiarMensaje("¡BASTA!");
            }
            else
            {
                RespuestaJugador1 = obj;
                
                EnviarComando(new DatoEnviado { Comando = Comando.JugadaEnviada, DatoRespuestas = RespuestaJugador1 });
                ActualizarValor();
                PuedeJugarCliente = false;
                PuedeJugarServidor = false;
                CambiarMensaje("¡BASTA!");
            }
        }

        async Task ValidarRespuestas()
        {
            if(RespuestaJugador1!=null && RespuestaJugador2!=null)
            {
                if(RespuestaJugador1.Nombre!=RespuestaJugador2.Nombre)
                {
                    pNombre1 = 100;
                    pNombre2 = 100;
                }
                else if(RespuestaJugador1.Nombre == RespuestaJugador2.Nombre)
                {
                    pNombre1 = 50;
                    pNombre2 = 50;
                }
                else if(!RespuestaJugador1.Nombre.StartsWith(Letra.ToString())|| RespuestaJugador1.Nombre == null)
                {
                    pNombre1 = 0;
                }
                else if (!RespuestaJugador2.Nombre.StartsWith(Letra.ToString()))
                {
                    pNombre2 = 0;
                }
                
            }
        }
    }


    public class DatoEnviado
    {
        public Comando Comando { get; set; }
        public string Nombre { get; set; }
        public char LetraRandom { get; set; }

        public Respuestas DatoRespuestas { get; set; }
    }
}
