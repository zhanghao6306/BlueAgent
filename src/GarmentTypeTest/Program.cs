using System;
using System.Net.Sockets;
using System.Buffers.Binary;
using System.Threading.Tasks;

namespace GarmentTypeTest
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
                        // Test garment type selection for each category
                        for (ushort garmentType = 0; garmentType <= 15; garmentType++)
                        {
                            Console.WriteLine($"\nTesting garment type {garmentType}");
                            await WriteHoldingRegister(stream, 1, 2050, garmentType);
                            await ReadCoils(stream, 1, 1016, 15); // Read all garment type coils
                            await ReadCoil(stream, 1, 1090); // Read confirmation coil
                            await Task.Delay(500); // Delay between tests
                        }
                    }

                    Console.WriteLine("\nAll garment type tests completed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static async Task WriteHoldingRegister(NetworkStream stream, byte unitId, ushort address, ushort value)
        {
            // Modbus TCP Write Single Register (FC=0x06)
            var mbap = new byte[7];
            var pdu = new byte[5];

            // MBAP Header
            BinaryPrimitives.WriteUInt16BigEndian(mbap.AsSpan(0, 2), 1); // Transaction ID
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
            BinaryPrimitives.WriteUInt16BigEndian(mbap.AsSpan(0, 2), 1); // Transaction ID
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
            if (data.Length > 0 && data[0] == 0x01)
            {
                bool value = false;
                if (data.Length > 2)
                {
                    value = (data[2] & 0x01) != 0;
                }
                Console.WriteLine($"Coil {address}: {value}");
            }
        }

        static async Task ReadCoils(NetworkStream stream, byte unitId, ushort startAddress, ushort count)
        {
            // Modbus TCP Read Coils (FC=0x01)
            var mbap = new byte[7];
            var pdu = new byte[5];

            // MBAP Header
            BinaryPrimitives.WriteUInt16BigEndian(mbap.AsSpan(0, 2), 1); // Transaction ID
            BinaryPrimitives.WriteUInt16BigEndian(mbap.AsSpan(2, 2), 0); // Protocol ID
            BinaryPrimitives.WriteUInt16BigEndian(mbap.AsSpan(4, 2), 6); // Length (1 unitId + 5 PDU)
            mbap[6] = unitId;

            // PDU
            pdu[0] = 0x01; // Function Code: Read Coils
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), startAddress);
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), count);

            await stream.WriteAsync(mbap);
            await stream.WriteAsync(pdu);

            // Read response
            var header = new byte[7];
            await stream.ReadAsync(header);
            ushort dataLength = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
            var data = new byte[dataLength];
            await stream.ReadAsync(data);

            // Parse and display coil values
            if (data[0] == 0x01)
            {
                for (int i = 0; i < count; i++)
                {
                    int bytePos = i / 8;
                    int bitPos = i % 8;
                    bool value = false;
                    if (2 + bytePos < data.Length)
                    {
                        value = (data[2 + bytePos] & (1 << bitPos)) != 0;
                    }
                    Console.WriteLine($"Coil {startAddress + i}: {value}");
                }
            }
        }
    }
}