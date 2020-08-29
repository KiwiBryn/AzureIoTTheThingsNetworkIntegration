#include <LoRaWan.h>

unsigned char data[] = {0x53, 0x65, 0x65, 0x65, 0x64, 0x75, 0x69, 0x6E, 0x6F, 0x20, 0x4C, 0x6F, 0x52, 0x61, 0x57, 0x41, 0x4E};
char buffer[256];

void setup(void)
{
  SerialUSB.begin(9600);
  while (!SerialUSB);

  lora.init();

  memset(buffer, 0, 256);
  lora.getVersion(buffer, 256, 1);
  SerialUSB.print("Ver:");
  SerialUSB.print(buffer);

  memset(buffer, 0, 256);
  lora.getId(buffer, 256, 1);
  SerialUSB.print(buffer);
  SerialUSB.print("ID:");

  lora.setKey(NULL, NULL, "12345678901234567890123456789012");
  lora.setId(NULL, "1234567890123456", "1234567890123456");

  lora.setPort(10);

  lora.setDeciveMode(LWOTAA);
  lora.setDataRate(DR0, AS923);

  lora.setDutyCycle(false);
  lora.setJoinDutyCycle(false);

  lora.setPower(14);


  while (!lora.setOTAAJoin(JOIN, 10))
  {
    SerialUSB.println("");
  }
    SerialUSB.println( "Joined");
}

void loop(void)
{
  bool result = false;

  //result = lora.transferPacket("Hello World!", 10);
  result = lora.transferPacket(data, sizeof(data));

  if (result)
  {
    short length;
    short rssi;

    memset(buffer, 0, 256);
    length = lora.receivePacket(buffer, 256, &rssi);

    if (length)
    {
      SerialUSB.print("Length is: ");
      SerialUSB.println(length);
      SerialUSB.print("RSSI is: ");
      SerialUSB.println(rssi);
      SerialUSB.print("Data is: ");
      for (unsigned char i = 0; i < length; i ++)
      {
        SerialUSB.print("0x");
        SerialUSB.print(buffer[i], HEX);
        SerialUSB.print(" ");
      }
      SerialUSB.println();
    }
  }
  delay( 10000);
}
