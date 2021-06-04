# Testing Emails/URL's

* contactus-test@northampton.digital - Northampton's MailBot Functionality
* documents-test@northampton.digital - Northampton's Bundler Functionality 
* unitary-test@northampton.digital - West Northamptonshires Unitary UAT Email Functionality
* [West Northants Contact Us Form](https://northamptonshire-self.test.achieveservice.com/service/Contact_West_Northamptonshire_Council) - Wests UAT Contact Us Form
* [West Northants UAT](https://northamptonuat.q.jadu.net/q/login) - Wests UAT Jadu System

# Testing Examples

Here are some testing examples that can be used to test various functions within the MailBot

## West Northants Mail Bot Enquiry Types:

<span>&#63;</span> **General Daventry District Enquiries**
Good Afternoon, I am trying to get hold someone that can help me. I would like to set up a little clearance within Daventry and I didn't know if the council supplied any equipment to help with this? 

<span>&#63;</span> **General South Northants District Enquiries**
I have just moved into 1 Station Road Cogenhoe NN7 1LU and wanted to know if I am eligable for Single Persons discount on my council tax? 

<span>&#63;</span> **General Northampton Borough Enquiries**
There is a large amount of litter strewn across Abington Park after the runner weather over the weekend. 

<span>&#63;</span> **General County Council Enquiries**
I need some advice on what additional care or support the council provides to the elderly and vulnerable? My mother has recently come out of hospital and requires 24/7 care. Are the council able to support me with this ? She is currently residing in Kingsthorpe. 


## MailBot Testing Scenarios:

<span>&#9758;</span> Testing Norberts Ability to place a query with Jadu CXM and forward to sovereign council (End Status = Case Closed) 
1. Using gmail, iCloud, hotmail, yahoo etc send an email to the Testing Enviroment outlining one of the West Northants Mail Bot Enquiry Types.
2. Await for auto response to be receiving confirming your query has been passed to an agent
<br>
n.b if at this point you get an email asking for you location, this means that Norbert has not been able to ascertain a location within your email. You will need to reply with an address, area, town or postcode to receive an SLA Response. If you have still not got a confirmation email after providing the location to Norbert, this means he has passed your query onto a human.
<br>
<br>
3. Open the UAT system and search for the reference number received on your Auto Response<p>
<br>
<span>&#9745;</span> Has the First and Surname pulled over <I>(Taken from the email account)</I><p>
<span>&#9745;</span> Has the email address pulled over?<p>
<span>&#9745;</span> Has the email subject pulled over?<p>
<span>&#9745;</span> Have the enquiry details pulled over correctly? <I>(Sent an email trail? This will be included in the original email field)</I><p>
<span>&#9745;</span>Has Norbert ascertained "The Guildhalls" Service Area (Based on the NBC MailBot QnA)<p>
<span>&#9745;</span>Has Norbert ascertained the customers Sentiment?<p>
<span>&#9745;</span>Has Norbert provided a suggestion response (Based on NBC MailBot QnA)<p>
<span>&#9745;</span> Has Norbert started the first trances of ascertaining the location<p>
<span>&#9745;</span> Has Norbert defined the service area? <p>



## NBC Mail Bot Enquiry Types:

<span>&#63;</span> **Housing Enquiries**
* 100% -   What are the supporting documents I need to give for my Housing Application?
* 83.98% -  Can I have a list of documents please
* 51.03% - I've submitted my bank statement and ID, what else do I need to provide?
* 27.81% -  I have my passport - What else?


<span>&#63;</span> **County Enquiries**
* 100% - tip opening times
* 82.22% - When can I go to the tip?
* 76.55% - Are you finally opening the tips now?
* 64.85% - I have stuff that needs to go to the tip
* 56.49% - Do I need a waste permit to go to sixfields?


<span>&#63;</span> **Waste Enquiries**
* 100% - Missed Bin 
* 77.84 - Why did you collect my neighbours bin and not mine?
* 65.25% - can you explain why my bin wasnt emptied?
* 51.89% - Bin wasn’t picked up


<span>&#63;</span> **Events Enquiries**
* 100% - apply to hold an event
* 72.13% - I want to hold a picnic in the park
* 74.17% - How do I arrange a charity event?
* 49.13 - Can you tell me about Heritage days?


<span>&#63;</span> **Repairs Enquiries**
* 100% - Report a repair
* 75.36% - My tap is leaking 
* 68.89% - You’ve blocked my sink
* 53.24% - I need you to fix my fence 


<span>&#63;</span> **Rent Enquiries**

* 100% - Rent free weeks 
* 89% - I need to pay rent
* 66.16% - Why has my rent gone up?
* 51.49% - Why is my mums rent cheaper than mine? 

<span>&#63;</span> **Environmental Health Enquiries**
* 100% - What are the rules for lighting bonfires
* 71.66% - The bonfire stinks! 
* 59.69% - My Neighbour is burning things in his garden 
* 41.97% - Can you burn tyres in the garden?


<span>&#63;</span> **General Enquiries**
* 100% - Who is my councillor 
* 98% - What councillor for where
* 81.06% - I want to talk to my councillor
* 56.48% - How do I find out who has been elected


<span>&#63;</span> **Elections Enquiries**
* 100% - register to vote
* 66.09% - how do I get my name on the electoral roll to boost my credit score?
* 63.97% - Where can I go to vote? 
* 57.11% - Who should I vote for




