To increase Norberts confidence score on correctly ascertaining the Soverign Service Area, adaptions have been made to his functionality when dealing with contact us forms. 

As the customer is telling Norbert the service they require, Norbert doesn't have to do as much work. However, in some instances there is a broader "sub categories" that fall within that service. For instance Housing is an over-arch of Housing Applications, Housing Solutions & Options, Tenancy Management, Repairs, Private Sector and much more.

In light of this - there are some areas where Norbert already knows the overarching service area; or in Amazon Lex terms the intent. So Norbert is able to already link the customers enquiry; or in amazon Lex terms the utterance. 

We tell Norbert to "ignore all he knows" in certain areas. These are highlighted below in their intent format. 

For example if a customer selects Adult Social Care - Norbert will set the service intent (service area outcome) to that field.

If the table outlines Lex, think means we have asked Norbert to read his knowledge database and find the utterance (customer query) that best matches to that Intent (Service Area). He will return what ever he finds as the best match, which is what is used to define the Soverign Service Area. 



| Service Area | Lex Database or Specific Intent |
| --- | --- |
| `Adult Social Care` | County_AdultSocialServices |
| `Benefits` | District_Benefits |
| `Births, Marriages & Death Registration Service` | County_Registrars |
| `Blue Badges` | County_Registrars |
| `Business Rates` | District_BusinessRates |
| `Building Control` | District_BuildingControl |
| `Children's Social Care` | County_ChildSafeGuarding |
| `Coronavirus` | District_Covid |
| `Council Tax` | District_Revenues |
| `Environmental Issues` | **Lex** |
| `Housing` | **Lex** |
| `Libraries` | County_Libraries |
| `Parking` | District_Parking |
| `Planning` | **Lex** |
| `Roads and Highways` | County_Highways|
| `School Admissions and Schools` | County_SchoolAdmissions|
| `Transport` | County_SchoolTransport|
| `Waste and Recycling` | **Lex** |