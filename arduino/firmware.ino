int DIG1 = 13;
int start_us = 0;
int ms = 0;
int totms = 0;
int val = 0;
int running = 0;
int count = 0;
static const int TIMEUNIT = 1; // ms
static const int NOTIFYUNIT = 50; // ms

void setup() {
  pinMode(DIG1, INPUT); 
  Serial.begin(115200);
}

void loop()
{
  if(!running)
  {
    if(Serial.read() != '1')
      return;
    running = 1;
    count = 0;
    start_us = micros();
    ms = totms = 0;
    Serial.print("S");
  }
  else
  {
    if(Serial.read() == '0')
    {
      running = 0;
      Serial.print("P");
      return;
    }
  }
  int val1 = digitalRead(DIG1);
  if(val != val1)
  {
    count += 1;
    val = val1;
  }

  int now = micros();
  if((now - start_us) >= 1000)
  {
      ms += 1;
      totms += 1;
      start_us += 1000;
  }
  if(ms >= 50)
  {
    ms = 0;
    Serial.print("T");
    Serial.print(totms);
    Serial.print("C");
    Serial.print(count);
    if(totms == 30000)
      totms = 0;
  }
}
