'use client'

import AccountHelpPage from "../../components/help/account";
import Theme2016 from "../../components/theme2016";
import Head from "next/head";

const AccountHelp = props => {
    return (
        <Theme2016>
            <Head>
                <link rel="preload" href="/markdown/account.md" as="fetch" crossOrigin="anonymous" />
            </Head>
            <AccountHelpPage />
        </Theme2016>
    );
}

export const getStaticProps = () => {
    return {
        props: {
            title: 'Account Help - Marine',
            disableWebsiteTheming: true,
        },
    };
};

export default AccountHelp;