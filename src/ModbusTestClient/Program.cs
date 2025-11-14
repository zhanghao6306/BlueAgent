using System;
using System.Net.Sockets;
using System.Buffers.Binary;
using System.Threading.Tasks;

namespace ModbusTestClient
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
                        // Test 1: Write D2000=1 (Start signal)
                        Console.WriteLine("\nTest 1: Writing D2000=1");
                        await WriteHoldingRegister(stream, 1, 2000, 1);
                        await ReadCoils(stream, 1, 1000, 1);

                        // Test 2: Write D2005=1 (Client feedback acknowledgment)
                        await Task.Delay(1000);
                        Console.WriteLine("\nTest 2: Writing D2005=1");
                        await WriteHoldingRegister(stream, 1, 2005, 1);
                        await ReadCoils(stream, 1, 1015, 1);

                        // Test 3: Write D2001=1 (Foundation print complete)
                        await Task.Delay(1000);
                        Console.WriteLine("\nTest 3: Writing D2001=1");
                        await WriteHoldingRegister(stream, 1, 2001, 1);
                        await ReadCoils(stream, 1, 1001, 1);

                        // Test 4: Write D2006=1 (Soft start request)
                        await Task.Delay(1000);
                        Console.WriteLine("\nTest 4: Writing D2006=1");
                        await WriteHoldingRegister(stream, 1, 2006, 1);
                        await ReadCoils(stream, 1, 1013, 1);
                        await ReadHoldingRegisters(stream, 1, 2068, 1);

                        // Test 5: Write D2050=5 (Garment type E)
                        await Task.Delay(1000);
                        Console.WriteLine("\nTest 5: Writing D2050=5");
                        await WriteHoldingRegister(stream, 1, 2050, 5);
                        await ReadCoils(stream, 1, 1016, 15); // Read all garment type coils

                        // Test 6: Write D2007=1 (Soft stop request)
                        await Task.Delay(1000);
                        Console.WriteLine("\nTest 6: Writing D2007=1");
                        await WriteHoldingRegister(stream, 1, 2007, 1);
                        await ReadCoils(stream, 1, 1014, 1);
                        await ReadHoldingRegisters(stream, 1, 2068, 1);

                        // Test 7: Read platform running status registers
                        await Task.Delay(1000);
                        Console.WriteLine("\nTest 7: Reading platform running status registers (D2060-D2071)");
                        await ReadHoldingRegisters(stream, 1, 2060, 12);
                    }

                    Console.WriteLine("\nAll tests completed");
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
            Console.WriteLine($"Write response: {BitConverter.ToString(response)}");
        }

        static async Task ReadHoldingRegisters(NetworkStream stream, byte unitId, ushort startAddress, ushort count)
        {
            // Modbus TCP Read Holding Registers (FC=0x03)
            var mbap = new byte[7];
            var pdu = new byte[5];

            // MBAP Header
            BinaryPrimitives.WriteUInt16BigEndian(mbap.AsSpan(0, 2), 1); // Transaction ID
            BinaryPrimitives.WriteUInt16BigEndian(mbap.AsSpan(2, 2), 0); // Protocol ID
            BinaryPrimitives.WriteUInt16BigEndian(mbap.AsSpan(4, 2), 6); // Length (1 unitId + 5 PDU)
            mbap[6] = unitId;

            // PDU
            pdu[0] = 0x03; // Function Code: Read Holding Registers
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

            Console.WriteLine($"Read Holding Registers response: {BitConverter.ToString(header)} {BitConverter.ToString(data)}");
            
            // Parse and display register values
            if (data[0] == 0x03)
            {
                int byteCount = data[1];
                for (int i = 0; i < byteCount / 2; i++)
                {
                    ushort value = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(2 + i * 2, 2));
                    Console.WriteLine($"Register {startAddress + i}: {value}");
                }
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

            Console.WriteLine($"Read Coils response: {BitConverter.ToString(header)} {BitConverter.ToString(data)}");
            
            // Parse and display coil values
            if (data[0] == 0x01)
            {
                int byteCount = data[1];
                for (int i = 0; i < count; i++)
                {
                    int bytePos = i / 8;
                    int bitPos = i % 8;
                    bool value = (data[2 + bytePos] & (1 << bitPos)) != 0;
                    Console.WriteLine($"Coil {startAddress + i}: {value}");
                }
            }
        }
    }
}