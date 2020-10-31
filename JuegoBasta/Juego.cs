using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace JuegoBasta
{
    public enum Comando { NombreEnviado, JugadaEnviada }
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

        // PROPIEDADES RESPUESTAS
        public string Nombre1 { get; set; }
        public string Nombre2 { get; set; }
        public string Lugar1 { get; set; }
        public string Lugar2 { get; set; }
        public string Animal1 { get; set; }
        public string Animal2 { get; set; }
        public string Color1 { get; set; }
        public string Color2 { get; set; }
        public string Comida1 { get; set; }
        public string Comida2 { get; set; }

        public List<string> RespuestaJugador1 { get; set; }
        public List<string> RespuestaJugador2 { get; set; }

        HttpListener servidor;
        ClientWebSocket cliente;
        Dispatcher currentDispatcher;
        WebSocket webSocket;

        public Juego()
        {
            currentDispatcher = Dispatcher.CurrentDispatcher;
            IniciarCommand = new RelayCommand<bool>(IniciarPartida);
            JugarCommand = new RelayCommand<string>(Jugar);
            ElegirLetra();

        }

        private void Jugar(string obj)
        {
            if (cliente != null)
            {
                 List<string> respuestas1 = new List<string>();

                respuestas1.Add(Nombre1);
                respuestas1.Add(Lugar1); 
                respuestas1.Add(Color1); 
                respuestas1.Add(Comida1); 
                respuestas1.Add(Animal1); 

                EnviarComando(new DatoEnviado { Comando = Comando.JugadaEnviada, Dato = respuestas1 });
            }
            else
            {
                List<string> respuestas2 = new List<string>();

                respuestas2.Add(Nombre2); 
                respuestas2.Add(Lugar2); 
                respuestas2.Add(Color2);
                respuestas2.Add(Comida2); 
                respuestas2.Add(Animal2); 

                EnviarComando(new DatoEnviado { Comando = Comando.JugadaEnviada, Dato = respuestas2 });
            }
        }

        public void ElegirLetra()
        {
            Letra = (char)r.Next('a', 'z');
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
                EnviarComando(new DatoEnviado { Comando = Comando.NombreEnviado, Dato = Jugador1 });

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
        // CUANDO YA SE INICIÓ EL SERVIDOR Y EL CLIENTE
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

                if (tipoP == true)
                {
                    CrearPartida();
                }
                else
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

                    if (cliente != null)
                    {
                        switch (comandorecibido.Comando)
                        {
                            case Comando.NombreEnviado:
                                Jugador1 = (string)comandorecibido.Dato;
                                CambiarMensaje("Se ha conectado con el jugador " + Jugador1);
                                _ = currentDispatcher.BeginInvoke(new Action(() =>
                                {
                                    EnviarComando(new DatoEnviado { Comando=Comando.NombreEnviado, Dato=Jugador2});
                                    lobby.Hide();

                                    ventanaJuego = new JuegoBastaWindow();
                                    ventanaJuego.Title = "Cliente";
                                    ventanaJuego.DataContext = this;

                                    CambiarMensaje("Inicie juego");
                                    ventanaJuego.ShowDialog();
                                    lobby.Show();

                                }));

                                break;
                            case Comando.JugadaEnviada:
                                RespuestaJugador1 = (List<string>)comandorecibido.Dato;

                                break;
                        }
                    }
                    else
                    {
                        switch (comandorecibido.Comando)
                        {
                            case Comando.NombreEnviado:
                                Jugador2 = (string)comandorecibido.Dato;
                                CambiarMensaje("Se ha conectado con el jugador " + Jugador2);

                                _ = currentDispatcher.BeginInvoke(new Action(() =>
                                {
                                    lobby.Hide();
                                    JuegoBastaWindow ventanaJuego = new JuegoBastaWindow();
                                    ventanaJuego.Title = "Servidor";
                                    ventanaJuego.DataContext = this;
                                    CambiarMensaje("Inicie juego");
                                    ventanaJuego.ShowDialog();
                                    lobby.Show();
                                }));
                                break;
                            case Comando.JugadaEnviada:
                                RespuestaJugador2 = (List<string>)comandorecibido.Dato;
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (webSocket.State == WebSocketState.Aborted)
                {
                    lobby.Close();
                    ventanaJuego.Close();
                    MainVisible = true;
                    ActualizarValor("MainVisible");
                }
                else
                {
                    CambiarMensaje(ex.Message);
                }
            }

        }
    }
    public class DatoEnviado
    {
        public Comando Comando { get; set; }
        public object Dato { get; set; }
    }
}
