using System;
using System.Net.Sockets;
using System.Buffers.Binary;
using System.Threading.Tasks;

namespace TestRiseEdge
{
    class Program
    {
        private static ushort _transactionId = 1;
        static async Task Main(string[] args)
        {
            try
            {
                // Connect to Modbus server
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync("127.0.0.1", 5020);
                    Console.WriteLine("Connected to Modbus server");

                    using (var stream = client.GetStream())
                    {
                        // Test D2000 -> M1000 rise edge
                        Console.WriteLine("\nTesting D2000 -> M1000 rise edge");
                        await WriteHoldingRegister(stream, 1, 2000, 1);
                        await Task.Delay(100);
                        await ReadCoil(stream, 1, 1000);

                        // Test D2050 -> M1090 for garment type 0
                        Console.WriteLine("\nTesting D2050 -> M1090 for garment type 0");
                        Console.WriteLine("Writing D2050 = 0");
                        await WriteHoldingRegister(stream, 1, 2050, 0);
                        Console.WriteLine("Write completed");
                        await Task.Delay(100);
                        await ReadCoil(stream, 1, 1090);

                        // Test D2050 -> M1090 for garment type 1
                        Console.WriteLine("\nTesting D2050 -> M1090 for garment type 1");
                        Console.WriteLine("Writing D2050 = 1");
                        await WriteHoldingRegister(stream, 1, 2050, 1);
                        Console.WriteLine("Write completed");
                        await Task.Delay(100);
                        await ReadCoil(stream, 1, 1090);
                        await ReadCoil(stream, 1, 1016);
                    }

                    Console.WriteLine("\nAll rise edge tests completed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static async Task WriteHoldingRegister(NetworkStream stream, byte unitId, ushort address, ushort value)
        {
            // Modbus TCP Write Single Register (FC=0x06)
            var mbap = new byte[7];
            var pdu = new byte[5];

            // MBAP Header
            BinaryPrimitives.WriteUInt16BigEndian(mbap.AsSpan(0, 2), _transactionId++); // Incrementing Transaction ID
            BinaryPrimitives.WriteUInt16BigEndian(mbap.AsSpan(2, 2), 0); // Protocol ID
            BinaryPrimitives.WriteUInt16BigEndian(mbap.AsSpan(4, 2), 6); // Length (1 unitId + 5 PDU)
            mbap[6] = unitId;

            // PDU
            pdu[0] = 0x06; // Function Code: Write Single Register
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), value);

            await stream.WriteAsync(mbap);
            await stream.WriteAsync(pdu);

            // Read response
            var response = new byte[12];
            await stream.ReadAsync(response);
        }

        static async Task ReadCoil(NetworkStream stream, byte unitId, ushort address)
        {
            // Modbus TCP Read Coils (FC=0x01)
            var mbap = new byte[7];
            var pdu = new byte[5];

            // MBAP Header
            BinaryPrimitives.WriteUInt16BigEndian(mbap.AsSpan(0, 2), _transactionId++); // Incrementing Transaction ID
            BinaryPrimitives.WriteUInt16BigEndian(mbap.AsSpan(2, 2), 0); // Protocol ID
            BinaryPrimitives.WriteUInt16BigEndian(mbap.AsSpan(4, 2), 6); // Length (1 unitId + 5 PDU)
            mbap[6] = unitId;

            // PDU
            pdu[0] = 0x01; // Function Code: Read Coils
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), 1); // Read 1 coil

            await stream.WriteAsync(mbap);
            await stream.WriteAsync(pdu);

            // Read response
            var header = new byte[7];
            await stream.ReadAsync(header);
            ushort dataLength = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
            var data = new byte[dataLength];
            await stream.ReadAsync(data);

            // Parse and display coil value
            if (data.Length > 0 && data[0] == 0x01 && data.Length > 2)
            {
                bool value = (data[2] & 0x01) != 0;
                Console.WriteLine($"Coil {address}: {value}");
            }
        }
    }
}