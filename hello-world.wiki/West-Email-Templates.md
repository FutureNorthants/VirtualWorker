There are various auto template emails that are sent by Norbert to our customers. These templates hold various tags which enables us to personalise template responses.

Tags used:

* **NNN** = BOTS Sign Off name (either Norbert or Norberta) or if over took by an agents – agents name
* **000** = Customers email body which was viewed by the bot and any customer updates where the customer has had to provide an updated location. If a location update is not available – just the query will show
* ***KKK*** = Customers email address 
* ***AAA*** = Spam/etc
* ***CCC*** = Agent Response 
* ***MMMTTTPPP*** = Customers email address, address and telephone number provided within the West Northants Council Us Form

# [Location Request](https://github.com/FutureNorthants/EmailTemplates/blob/master/email-location-request.txt)

**Purpose:** A location request template is automatically sent to customers using either email or west’s online web form when either of the following rules are met.

* A postcode or town is not recognised or provided within Northamptonshire within the email body
* A postcode or town is not recognised or provided within the contact form address or enquiry field. 

**Tags:** 

* **NNN** = BOTS Sign Off name (either Norbert or Norberta) or if over took by an agents – agents name



**Template:**

<I>Thank you for contacting West Northamptonshire Council.</I>

<I>We require further information in order to proceed with your enquiry.</I>


<I>Please can you confirm the location, including the town or postcode, of the issue you are reporting. If you do not have this information, please confirm the town or postcode for where you live.</I>

<I>This information will enable us to allocate your contact correctly.</I>

<I>Please note we are unable to resolve your enquiry until this information is received. If we do not receive this within five working days, we will assume that you no longer require this service.</I>

<I>Alternatively, the council’s online facilities are available around the clock at www.westnorthants.gov.uk. 
Kind Regards,</I>

<I>***NNN*** - your Virtual Assistant</I><p>

<I>Please note if your enquiry is urgent, you can contact us on 0300 126 7000. You can find further information on West Northamptonshire Council's Privacy Policy and how we process your personal information by visiting www.westnorthants.gov.uk/privacy.</I>


# [Sovereign Auto Acknowledgement:](https://github.com/FutureNorthants/EmailTemplates/blob/master/email-sovereign-acknowledge.txt)


**Purpose:** When a contact has been received by a human process – the customer will receive an auto acknowledgment confirming that their query will be dealt with within 5 working day. It also reminds the customer of what their original query was – in case of multiple 


**Tags:**
* **NNN** = BOTS Sign Off name (either Norbert or Norberta) or if over took by an agents – agents name
* **000** = Customers email body which was viewed by the bot and any customer updates where the customer has had to provide an updated location. If a location update is not available – just the query will show



**Template:**

<I>Thank you for contacting West Northamptonshire Council.</I>

<I>I can confirm your email has been passed to one of my colleagues in our customer service centres to review. You will receive a response within the next 5 working days.</I>

<I>Your original query to me was </I> 

***000***

<I>Kind Regards,</I>

<I>***NNN*** - your Virtual Assistant</I>

<I>Please note if your enquiry is urgent, you can contact us on 0300 126 7000. You can find further information on West Northamptonshire Council's Privacy Policy and how we process your personal information by visiting www.westnorthants.gov.uk/privacy.</I>


# [Sovereign Forward Emails:](https://github.com/FutureNorthants/EmailTemplates/blob/master/email-sovereign-forward.txt)

**Purpose:** All information provided by the customer within their email can be given to the triaged sovereign authority/service area


**Tags:**

* **NNN** = BOTS Sign Off name (either Norbert or Norberta) or if over took by an agents – agents name
* **000** = Customers email body which was viewed by the bot and any customer updates where the customer has had to provide an updated location. If a location update is not available – just the query will show
* ***KKK*** = Customers email address used when contacting contactus@westnorthants.gov.uk 


**Template:**
<I>Dear sovereign colleague</I>

<I>The enquiry below was received by West Northamptonshire Email Hub and requires action by your service.</I>

<I>The customer's query and responses (if any) were - ***000***</I>

<I>The case has been automatically closed on our systems.</I> 

<I>The customer's email address is -  ***KKK***</I>


<I>Please can you respond directly to the customer to resolve their query.</I>

<I>Kind Regards</I> 
<I>Norbert</I>



# [Sovereign Contact Us Emails:](https://github.com/FutureNorthants/EmailTemplates/blob/master/email-sovereign-forward-contactus.txt)


**Purpose:** All information provided by the customer within the West Contact Us form can be given to the triaged sovereign authority/service area.


**Tags:**

* **NNN** = BOTS Sign Off name (either Norbert or Norberta) or if over took by an agents – agents name
* **000** = Customer enquiry field which was viewed by the bot and any customer updates where the customer has had to provide an updated location. If a location update is not available – just the query will show
* **MMMTTTPP** = Customers email address , Customer address provided on the West contact us form and telephone number provided.


**Template**

<I>Dear sovereign colleague</I> 

<I>The enquiry below was received by West Northamptonshire Email Hub and requires action by your service.</I>

<I>The customer's query and responses (if any) were - ***OOOO***</I>

<I>The case has been automatically closed on our systems.</I>

***MMMTTTPPP***

<I>Please can you respond directly to the customer to resolve their query.</I>

<I>Kind Regards</I>
<I>Norbert</I>


# [Unsafe Rejection:](https://github.com/FutureNorthants/EmailTemplates/blob/master/email-unsafe-rejection.txt)

**Purpose:** Where a query has been detected as holding either spam, explicit content – the sender will be advised that their contact has been rejected and they will be redirected to our website.

**Tags:**

* ***AAA*** = Spam/etc
* ***NNN*** = BOTS Sign Off name (either Norbert or Norberta) or if over took by an agents – agents name


**Template**

<I>Thank you for emailing West Northamptonshire Council. I am afraid your email has been rejected by our systems as it was identified as containing ***AAA***.</I>

<I>Please visit our Contact Us web page for other ways to get in touch.</I>

<I>Kind Regards,</I> 

<I>***NNN*** - your Northampton Borough Council virtual assistant</I>

<I>Please note if your enquiry is urgent, you can contact us on 0300 126 7000. You can find further information on West Northamptonshire Council's Privacy Policy and how we process your personal information by visiting www.westnorthants.gov.uk/privacy.</I>





# [Unitary Misdirect:](https://github.com/FutureNorthants/EmailTemplates/blob/master/sovereign-misdirect-acknowledge.txt)


**Purpose:** When a customer has contacted West Northamptonshire Council however our bot has ascertained that they fall within North Northamptonshire – their query will be automatically processed and sent to service area within North Northamptonshire. The customer is advised of this in line the general data protection regulations. 

**Tags: **

* N/A


**Template**

<I>Thank you for contacting our Customer Service team at West Northamptonshire Council.</I>

<I>We have noted that your enquiry should be directed to North Northamptonshire Council and therefore we have forwarded this enquiry on, for your convenience. We are advising you of this action in line with our requirements under the right to be informed in the General Data Protection Regulations.</I>

<I>For future queries relating to North Northamptonshire Council please visit www.northnorthants.gov.uk.</I> 

<I>You can find further information on West Northamptonshire Council's Privacy Policy and how we process your personal information by visiting www.westnorthants.gov.uk/privacy.</I>

<I>Kind Regards</I>
<I>Customer Services</I>
<I>West Northamptonshire Council</I>




# [Re-Open Sovereign Acknowledgment](https://github.com/FutureNorthants/EmailTemplates/blob/master/email-reopen.txt)



**Purpose:** If the customer updates their case after it has been closed, they will receive an auto acknowledgement.

**Tags:**

* ***NNN*** = BOTS Sign Off name (either Norbert or Norberta) or if overtook by an agents – agents name
* ***000*** = Customer enquiry field which was viewed by the bot and any updates provided from the customer 
* ***AAA*** = Field used to identify the enquiry type (spam, virus etc) 


**Template**

<I>Thank you for getting back in touch with West Northamptonshire Council. I have updated your case AAA and assigned it to one of my colleges to review. They will respond to your via email within the next 5 working days.</I>
 
<I>Your original query to me was -</I>

<I>***000***</I>

<I>Kind Regards,</I>

<I>***NNN*** - your Virtual Assistant<I> 

<I>Please note if your enquiry is urgent, you can contact us on 0300 126 7000. You can find further information on West Northamptonshire Council's Privacy Policy and how we process your personal information by visiting www.westnorthants.gov.uk/privacy.</I>
























# Email Hub Response Case Closed


**Purpose:** To enable the customer to see the original response and be aware their case has now been closed


**Tags:**
* ***NNN*** = BOTS Sign Off name (either Norbert or Norberta) or if over took by an agents – agents name
* ***000*** = Customer enquiry field which was viewed by the bot and any customer updates where the
* ***CCC*** = Agent Response 


**Template**

<I>Thank you for contacting West Northamptonshire Council.

***CCC***

Your original query to me was :

***000*** 

If you need no further assistance this case will be closed automatically. 

Kind Regards,

***NNN***
Please note if your enquiry is urgent, you can contact us on 0300 126 7000. You can find further information on West Northants Privacy Policy and how we process your personal information by visiting www.westnorthants.gov.uk/privacy.</I>
























# Email Hub Response Further Information

**Purpose:** To enable the customer to see the original response and be aware we require further information on their original enquiry. 



**Tags:**
* ***NNN*** = BOTS Sign Off name (either Norbert or Norberta) or if over took by an agents – agents name
* ***000*** = Customer enquiry field which was viewed by the bot and any customer updates where the
* ***CCC*** = Agent Response 

<I>Thank you for contacting West Northamptonshire Council. In order to proceed with dealing with your enquiry I require some further information from yourself. 

***CCC***

Your original query to me was :

***000*** 

Please can I request you respond to this email directly.

Kind Regards,

***NNN***
Please note if your enquiry is urgent, you can contact us on 0300 126 7000. You can find further information on West Northants Privacy Policy and how we process your personal information by visiting www.westnorthants.gov.uk/privacy.</I>
































