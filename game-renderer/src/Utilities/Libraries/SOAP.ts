import {v4 as uuidv4} from "uuid";

export const SOAP = (baseUrl: string, jobExpiration: number, finalScript: string) => {
    // REQUIRES leading slash (for some reason)
    return `
    <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ns1="${baseUrl}">
   <soapenv:Header/>
   <soapenv:Body>
      <ns1:BatchJob>
         <ns1:job>
            <ns1:id>${uuidv4().toString()}</ns1:id>
            <ns1:expirationInSeconds>${jobExpiration}</ns1:expirationInSeconds>
            <ns1:cores>1</ns1:cores>
         </ns1:job>
         <ns1:script>
            <ns1:name>${uuidv4().toString()}</ns1:name>
            <ns1:script><![CDATA[
                ${finalScript}
            ]]></ns1:script>
            ${/*<arguments>
                {Arguments}
            </arguments>*/null}
         </ns1:script>
      </ns1:BatchJob>
   </soapenv:Body>
</soapenv:Envelope>
`;
};
