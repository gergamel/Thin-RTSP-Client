using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Threading;
using Media.Rtsp;


namespace RTSP_Client_Test
{
	public partial class Form1 : Form
	{
		Thread ClientThreadProc;
		RtspClient m_rtspClient;

		public Form1()
		{
			InitializeComponent();
		}

		private void PlayButton_Click(object sender, EventArgs e)
		{
			if (ClientThreadProc != null)
			{
				if (m_rtspClient != null)
				{
					m_rtspClient.StopPlaying();
					m_rtspClient = null;
				}

				Media.Common.Extensions.Thread.ThreadExtensions.TryAbortAndFree(ref ClientThreadProc);

				ClientThreadProc = null;

				GC.WaitForPendingFinalizers();

				PlayButton.Text = "Start";
			}
			else
			{

				m_rtspClient = new RtspClient(URLTextBox.Text, RtspClient.ClientProtocolType.Tcp);

				//Client.DisableKeepAliveRequest = checkBox1.Checked;

				m_rtspClient.OnConnect += RTSPClient_OnConnect;

				ClientThreadProc = new Thread(() => m_rtspClient.Connect());

				ClientThreadProc.Start();

				PlayButton.Text = "Stop";
			}
		}

		private void RTSPClient_OnConnect(RtspClient sender, object args)
		{
			sender.OnPlay += RTSPClient_OnPlay;
			sender.OnRequest += RTSPClient_OnRequest;
			sender.OnResponse += RTSPClient_OnResponse;

			try
			{
				sender.StartPlaying();
			}
			catch
			{
				return;
			}
		}

		private void RTSPClient_OnPlay(RtspClient sender, object args)
		{
			sender.OnDisconnect += RTSPClient_OnDisconnect;
			sender.Client.RtpPacketReceieved += RTSPClient_RtpPacketReceieved;
			sender.Client.RtcpPacketReceieved += RTSPClient_RtcpPacketReceieved;

			sender.Client.RtcpPacketSent += RTSPClient_RtcpPacketSent;
			//sender.Client.RtpFrameChanged += RTSPClient_RtpFrameChanged;

			sender.Client.AverageMaximumRtcpBandwidthPercentage = 0;// (double)numericUpDown2.Value;

			sender.OnStop += RTSPClient_OnStop;
		}

		private void RTSPClient_OnRequest(RtspClient sender, RtspMessage request)
		{
			if (InvokeRequired)
			{
				MethodInvoker del = delegate { RTSPClient_OnRequest(sender, request); };
				Invoke(del);
			}
			else
			{
				if (request != null)
				{
					if (m_rtspClient == null || m_rtspClient.IsDisposed)
					{
						return;
					}

					//Disable keep alives if indicated
					//m_rtspClient.DisableKeepAliveRequest = checkBox1.Checked;

//					RTSPMessagesTextBox.AppendText("RTSPClient_OnRequest" + Environment.NewLine);
//					RTSPMessagesTextBox.AppendText("@" + request.Created.ToUniversalTime().ToString() + " - " + request.ToString() + Environment.NewLine);
					
					if (m_rtspClient.IsPlaying)
					{
						PlayButton.Text = "(Playing)STOP";

						if (!m_rtspClient.LivePlay)
						{
							label2.Text = "Remaining: " + (DateTime.UtcNow - m_rtspClient.StartedPlaying.Value).Subtract(m_rtspClient.EndTime.Value).ToString();
						}
					}
					else
					{
						PlayButton.Text = "STOP";
						if (m_rtspClient.LivePlay)
						{
							label2.Text = "Live Play";
						}
						else
						{
							label2.Text = "Done Playing";
						}
					}
					
				}
			}
		}

		private void RTSPClient_OnResponse(RtspClient sender, RtspMessage request, RtspMessage response)
		{
			if (InvokeRequired)
			{
				MethodInvoker del = delegate { RTSPClient_OnResponse(sender, request, response); };
				Invoke(del);
			}
			else
			{
				if (request != null && response != null)
				{

					//Disable keep alives if indicated
					//m_rtspClient.DisableKeepAliveRequest = checkBox1.Checked;

					RTSPMessagesTextBox.AppendText("RTSPClient_OnResponse (Request)" + Environment.NewLine);
					RTSPMessagesTextBox.AppendText("@" + request.Created.ToUniversalTime().ToString() + " - " + request.ToString() + Environment.NewLine);

					RTSPMessagesTextBox.AppendText("RTSPClient_OnResponse (Response)" + Environment.NewLine);
					RTSPMessagesTextBox.AppendText("@" + response.Created.ToUniversalTime().ToString() + " - " + response.ToString() + Environment.NewLine);
				}
			}
		}

		private void RTSPClient_OnDisconnect(RtspClient sender, object args)
		{
			if (sender == null || sender.IsDisposed) return;

			//			sender.Client.RtpPacketReceieved -= RTSPClient_RtpPacketReceieved;
			//			sender.Client.RtcpPacketReceieved -= RTSPClient_RtcpPacketReceieved;


			sender.OnPlay -= RTSPClient_OnPlay;

			sender.OnResponse -= RTSPClient_OnResponse;
			sender.OnRequest -= RTSPClient_OnRequest;

			sender.OnDisconnect -= RTSPClient_OnDisconnect;
			sender.Client.RtpPacketReceieved -= RTSPClient_RtpPacketReceieved;
			sender.Client.RtcpPacketReceieved -= RTSPClient_RtcpPacketReceieved;

			sender.Client.RtcpPacketSent -= RTSPClient_RtcpPacketSent;

			sender.OnStop -= RTSPClient_OnStop;


			sender.Dispose();
			PlayButton_Click(this, EventArgs.Empty);
		}

		private void RTSPClient_OnStop(RtspClient sender, object args)
		{
			
		}

		private void RTSPClient_RtcpPacketSent(object sender, Media.Rtcp.RtcpPacket packet = null, Media.Rtp.RtpClient.TransportContext tc = null)
		{
			if (InvokeRequired)
			{
				MethodInvoker del = delegate { RTSPClient_RtcpPacketSent(sender, packet, tc); };
				Invoke(del);
			}
			else
			{
				if (!m_rtspClient.LivePlay)
				{
					label2.Text = "Remaining: " +
						(DateTime.UtcNow - m_rtspClient.StartedPlaying.Value).Subtract(m_rtspClient.EndTime.Value).ToString();
				}
				/*
				RTSPMessagesTextBox.AppendText("RTSPClient_RtcpPacketSent" + Environment.NewLine);
				RTSPMessagesTextBox.AppendText("@" + packet.Created.ToUniversalTime().ToString() + " - " + packet.ToString() + Environment.NewLine);
				*/
			}
		}

		private void RTSPClient_RtcpPacketReceieved(object sender, Media.Rtcp.RtcpPacket packet = null, Media.Rtp.RtpClient.TransportContext tc = null)
		{
			if (InvokeRequired)
			{
				MethodInvoker del = delegate { RTSPClient_RtcpPacketReceieved(sender, packet, tc); };
				Invoke(del);
			}
			else
			{
				if (!m_rtspClient.LivePlay)
				{
					label2.Text = "Remaining: " +
						(DateTime.UtcNow - m_rtspClient.StartedPlaying.Value).Subtract(m_rtspClient.EndTime.Value).ToString();
				}
				/*
				RTSPMessagesTextBox.AppendText("RTSPClient_RtcpPacketReceieved" + Environment.NewLine);
				RTSPMessagesTextBox.AppendText("@" + packet.Created.ToUniversalTime().ToString() + " - " + packet.ToString() + Environment.NewLine);

				RTSPMessagesTextBox.AppendText("IsComplete = " + packet.IsComplete.ToString() + ", " );
				RTSPMessagesTextBox.AppendText("IsCompressed = " + packet.IsCompressed.ToString() + ", " );
				RTSPMessagesTextBox.AppendText("Length = " + packet.Length.ToString() + ", " );
				RTSPMessagesTextBox.AppendText("PayloadType = " + packet.PayloadType.ToString() + ", " + Environment.NewLine);
				*/
			}
		}

		private void RTSPClient_RtpPacketReceieved(object sender, Media.Rtp.RtpPacket packet = null, Media.Rtp.RtpClient.TransportContext tc = null)
		{
			if (InvokeRequired)
			{
				MethodInvoker del = delegate { RTSPClient_RtpPacketReceieved(sender, packet, tc); };
				Invoke(del);
			}
			else
			{
				if (!m_rtspClient.LivePlay)
				{
					label2.Text = "Remaining: " +
						(DateTime.UtcNow - m_rtspClient.StartedPlaying.Value).Subtract(m_rtspClient.EndTime.Value).ToString();
				}
				RTSPMessagesTextBox.AppendText("RTSPClient_RtpPacketReceieved" + Environment.NewLine);
				RTSPMessagesTextBox.AppendText("@" + packet.Created.ToUniversalTime().ToString() + " - " + packet.ToString() + Environment.NewLine);

				RTSPMessagesTextBox.AppendText("IsComplete = " + packet.IsComplete.ToString() + ", ");
				RTSPMessagesTextBox.AppendText("IsCompressed = " + packet.IsCompressed.ToString() + ", ");
				RTSPMessagesTextBox.AppendText("Length = " + packet.Length.ToString() + ", ");
				RTSPMessagesTextBox.AppendText("SequenceNumber = " + packet.SequenceNumber.ToString() + ", ");
				RTSPMessagesTextBox.AppendText("PayloadType = " + packet.PayloadType.ToString() + ", " + Environment.NewLine);
				
#if DRIBBLE
				try
				{
					int count = 0;
					int byteCount = 40;

					foreach (byte payloadByte in packet.PayloadData)
					{
						RTSPMessagesTextBox.AppendText(String.Format("{0:X2}", payloadByte));
						RTSPMessagesTextBox.AppendText(" ");
						if(count++ > byteCount)
						{
							break;
						}
					}

					RTSPMessagesTextBox.AppendText(Environment.NewLine);
				}
				catch (Exception ex)
				{
					RTSPMessagesTextBox.AppendText("Image Exception:" + ex.Message + Environment.NewLine);
				}
#endif
				
				RTSPMessagesTextBox.AppendText(Environment.NewLine);
			}
		}

		
		private void RTSPClient_RtpFrameChanged(object sender, Media.Rtp.RtpFrame frame = null, Media.Rtp.RtpClient.TransportContext tc = null, bool final = false)
		{
			if (InvokeRequired)
			{
				MethodInvoker del = delegate { RTSPClient_RtpFrameChanged(sender, frame, tc, final); };
				Invoke(del);
			}
			else
			{
				RTSPMessagesTextBox.AppendText("RTSPClient_RtpFrameChanged" + Environment.NewLine);
				RTSPMessagesTextBox.AppendText("@" + frame.Created.ToUniversalTime().ToString() + " - " + frame.ToString() + Environment.NewLine);

				frame.Depacketize();

				//If the frame has depacketize data
				if (frame.HasDepacketized)
				{
					RTSPMessagesTextBox.AppendText("BufferSize = " + frame.Buffer.Length.ToString() + Environment.NewLine);
					RTSPMessagesTextBox.AppendText("Packets = " + frame.Count.ToString() + Environment.NewLine);
					
					System.IO.Stream buffer = frame.Buffer;

					/*
					//Check for the expected length
					if (buffer.Length != frameCount) throw new Exception("More data in buffer than expected");

					//Read the buffer
					while (buffer.Position < frameCount)
					{
						//If the byte is out of order then throw an exception
						if (buffer.ReadByte() != expected++) throw new Exception("Data at wrong position");
					}
					*/
				}
				else
				{
					RTSPMessagesTextBox.AppendText("Not DEPACKETIZED" + Environment.NewLine);
				}

			}
		}

	}
}
