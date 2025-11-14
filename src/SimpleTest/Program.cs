using System;
using System.Net.Sockets;
using System.Buffers.Binary;
using System.Threading.Tasks;

namespace SimpleTest
{
    class Program
    {
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
                        // Write D2050 = 0
                        Console.WriteLine("Writing D2050 = 0");
                        await WriteHoldingRegister(stream, 1, 2050, 0);
                        await Task.Delay(500);

                        // Read M1090
                        Console.WriteLine("Reading M1090");
                        bool value = await ReadCoil(stream, 1, 1090);
                        Console.WriteLine($"M1090 = {value}");
                    }

                    Console.WriteLine("Test completed");
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
            var request = new byte[12];

            // MBAP Header (7 bytes)
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(0, 2), 1); // Transaction ID
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), 0); // Protocol ID
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), 6); // Length (1 unitId + 5 PDU)
            request[6] = unitId;

            // PDU (5 bytes)
            request[7] = 0x06; // Function Code: Write Single Register
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(8, 2), address);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(10, 2), value);

            await stream.WriteAsync(request);

            // Read response
            var response = new byte[12];
            int read = await stream.ReadAsync(response);
            if (read < 12) throw new Exception("Incomplete response");

            // Validate response
            if ((response[7] & 0x80) != 0) // Check if exception
            {
                byte exceptionCode = response[8];
                throw new Exception($"Modbus exception {exceptionCode}");
            }

            // Validate response data
            ushort responseAddress = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(8, 2));
            ushort responseValue = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(10, 2));
            if (responseAddress != address || responseValue != value)
            {
                throw new Exception("Response data mismatch");
            }
        }

        static async Task<bool> ReadCoil(NetworkStream stream, byte unitId, ushort address)
        {
            // Modbus TCP Read Coils (FC=0x01)
            var request = new byte[12];

            // MBAP Header
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(0, 2), 1); // Transaction ID
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), 0); // Protocol ID
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), 6); // Length (1 unitId + 5 PDU)
            request[6] = unitId;

            // PDU
            request[7] = 0x01; // Function Code: Read Coils
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(8, 2), address);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(10, 2), 1); // Read 1 coil

            await stream.WriteAsync(request);

            // Read response with timeout
            stream.ReadTimeout = 5000; // Set read timeout to 5 seconds
            var response = new byte[10];
            int read = await stream.ReadAsync(response);
            if (read < 10) throw new Exception($"Incomplete response: {read} bytes read");

            // Check if exception
            if ((response[7] & 0x80) != 0)
            {
                byte exceptionCode = response[8];
                throw new Exception($"Modbus exception {exceptionCode}");
            }

            // Validate response
            if (response[7] != 0x01) throw new Exception("Function code mismatch");
            if (response[8] != 1) throw new Exception("Byte count mismatch");

            return (response[9] & 0x01) != 0;
        }
    }
}