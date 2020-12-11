import React, { useEffect, useState } from 'react';
import  * as BS from 'react-bootstrap'

import MemberCard from './MemberCard.js'
import Loading from "./Loading.js";
import API_URL from "../Constants/constants.js";

export default function Memberlist(props) {
    
    const user = JSON.parse(localStorage.getItem('user'));
    const userId = user.id;

    const [isLoading, setIsLoading ] = useState(false);
    const [isError, setIsError ] = useState(false);
    const [members, setMembers ] = useState([]);
    const [value, setValue] = useState('')


    useEffect(() => {
        setIsLoading(true);
        setIsError(false);

         fetch(`${API_URL}s/${userId}/members`,{
        method: 'get',
        headers: {
          'Authorization': JSON.stringify(localStorage.getItem("token")).slice(1, -1)
        }}).then ( res => res.json()
        ).then (data => { 
          setMembers(data);
          setIsLoading(false);
      })
        .catch (error => { 
            console.log(error);
            setIsError(true);
            setIsLoading(false);
        })
    }, [userId])
    

    const memberList = members.filter(member => {
        if (!value) return true;
        if (member.name.toLowerCase().includes(value.toLowerCase())) {
          return true;
        }
        return false;
      }).sort((a, b) => a.name.localeCompare(b.name)).map((member) => <BS.Card key={member.id}>
        <MemberCard
        member={member} 
        />
    </BS.Card>
    );

    return (
        <>
        { isLoading ? <Loading /> : isError ? 
        <BS.Alert variant="danger">Error fetching members.</BS.Alert> :
        <>
        <BS.Row className="justify-content-md-center">
        <BS.Col xs={12} lg={4}>
        <BS.Form inline>
            <BS.Form.Control className="w-100" value={value} onChange={e => setValue(e.target.value)} placeholder="Search"/>
        </BS.Form>
        </BS.Col>
        </BS.Row>
        <BS.Accordion className="mb-3 mt-3 w-100" defaultActiveKey="0">
            {memberList}
        </BS.Accordion>
        </>
        }
        </>
    )
}